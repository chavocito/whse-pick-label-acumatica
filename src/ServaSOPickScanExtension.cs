using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using PX.BarcodeProcessing;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.IN.WMS;
using PX.Objects.SO;
using PX.Objects.SO.WMS;

namespace Serva.Acumatica.SoScannerLabels
{
    using WMSBase = WarehouseManagementSystem<PickPackShip, PickPackShip.Host>;

    public class ServaSOPickScanExtension : PickPackShip.ScanExtension
    {
        public static bool IsActive() => true;

        public PXAction<ScanHeader> ServaSelectPrinter;
        [PXButton(CommitChanges = true, DisplayOnMainToolbar = false)]
        [PXUIField(DisplayName = "Print Label")]
        protected virtual IEnumerable servaSelectPrinter(PXAdapter adapter)
        {
            PrintWithEffectivePrinter();
            return adapter.Get();
        }

        [PXOverride]
        public virtual ScanMode<PickPackShip> DecorateScanMode(
            ScanMode<PickPackShip> original,
            Func<ScanMode<PickPackShip>, ScanMode<PickPackShip>> baseDecorateScanMode)
        {
            ScanMode<PickPackShip> mode = baseDecorateScanMode(original);
            if (mode is PickPackShip.PickMode pickMode)
            {
                pickMode
                    .Intercept.CreateStates.ByAppend(basis => new ScanState<PickPackShip>[]
                    {
                        new LabelCountState(),
                        new LabelQuantityState(),
                        new PrinterSelectionState()
                    })
                    .Intercept.CreateCommands.ByAppend(() => new[] { new SelectPrinterCommand() })
                    .Intercept.ResetMode.ByAppend((basis, fullReset) => ClearPendingLabel(basis));
            }
            return mode;
        }

        [PXOverride]
        public virtual ScanState<PickPackShip> DecorateScanState(
            ScanState<PickPackShip> original,
            Func<ScanState<PickPackShip>, ScanState<PickPackShip>> baseDecorateScanState)
        {
            ScanState<PickPackShip> state = baseDecorateScanState(original);
            if (state is PickPackShip.PickMode.ConfirmState confirmState)
            {
                confirmState.Intercept.PerformConfirmation.ByOverride((basis, basePerformConfirmation) =>
                {
                    SOShipLineSplit split = basis
                        .Get<PickPackShip.PickMode.ConfirmState.Logic>()
                        .GetSplitsToPick()
                        .FirstOrDefault();
                    SOShipLine line = split == null
                        ? null
                        : SelectFrom<SOShipLine>
                            .Where<SOShipLine.shipmentNbr.IsEqual<@P.AsString>
                                .And<SOShipLine.lineNbr.IsEqual<@P.AsInt>>>
                            .View.ReadOnly.Select(basis.Graph, split.ShipmentNbr, split.LineNbr);
                    string shipmentNbr = split?.ShipmentNbr ?? basis.RefNbr;
                    int? lineNbr = line?.LineNbr ?? split?.LineNbr;
                    int? locationID = basis.LocationID ?? split?.LocationID;
                    decimal? pickedQty = basis.Qty;
                    string uom = basis.UOM;
                    bool positivePick = basis.Remove != true && basis.BaseQty > 0m;

                    FlowStatus result = basePerformConfirmation();
                    if (!positivePick || result.IsError != false)
                        return result;

                    basis.DispatchNext();
                    string contextError = line?.InventoryID == null
                        ? "The committed shipment line context was not captured for label printing."
                        : null;
                    try
                    {
                        PXGraph.CreateInstance<ServaLabelPrintService>().CreatePickRequest(
                            shipmentNbr,
                            lineNbr,
                            locationID,
                            pickedQty,
                            uom,
                            line?.OrigOrderType,
                            line?.OrigOrderNbr,
                            line?.InventoryID ?? split?.InventoryID ?? basis.InventoryID,
                            line?.SiteID ?? split?.SiteID ?? basis.SiteID,
                            line?.TranDesc,
                            null,
                            null,
                            contextError);

                        if (contextError == null)
                            basis.SetScanState<LabelCountState>();
                        else
                        {
                            basis.ReportError("The pick succeeded, but the label request failed: {0}", contextError);
                            basis.Get<ServaSOPickScanExtension>().ResumePicking();
                        }
                    }
                    catch (Exception ex)
                    {
                        basis.ReportError("The pick succeeded, but the label audit request could not be saved: {0}", ex.Message);
                        basis.Get<ServaSOPickScanExtension>().ResumePicking();
                    }
                    return FlowStatus.Ok;
                });
            }
            return state;
        }

        internal static ServaWMSScanHeaderExt ContextFor(PickPackShip basis) =>
            basis.Header.GetExtension<ServaWMSScanHeaderExt>();

        internal static void ClearPendingLabel(PickPackShip basis)
        {
            ServaWMSScanHeaderExt context = ContextFor(basis);
            context.UsrServaLabelCount = null;
            context.UsrServaLabelQuantity = null;
        }

        private void ResumePicking()
        {
            ClearPendingLabel(Basis);
            Basis.Clear<PickPackShip.LocationState>();
            Basis.SetScanState<PickPackShip.LocationState>();
        }

        internal virtual bool PrintWithEffectivePrinter()
        {
            var service = PXGraph.CreateInstance<ServaLabelPrintService>();
            ServaWMSScanHeaderExt context = ContextFor(Basis);
            Guid? printerID = context.UsrServaPrinterOverrideID ?? service.GetDefaultPrinterID();
            if (printerID == null)
            {
                Basis.ReportError("No printer is configured. Set Default Printer in SM203010 or scan a printer name, DeviceHub ID, or GUID.");
                return false;
            }

            ServaLabelPrintRequest pending = service.FindLatestPickRequest(Basis.RefNbr, awaitingLabelCount: false);
            if (pending == null)
                Basis.ReportInfo("No pending label request was found.");
            else
                SubmitAndResume(pending, printerID);
            return true;
        }

        internal virtual void SubmitPendingAndResume(Guid? printerID)
        {
            try
            {
                var service = PXGraph.CreateInstance<ServaLabelPrintService>();
                ServaLabelPrintRequest request = service.GetLatestPickRequest(Basis.RefNbr, awaitingLabelCount: false);
                SubmitAndResume(request, printerID);
            }
            catch (Exception ex)
            {
                Basis.ReportError("The pick succeeded, but label printing failed: {0}", ex.Message);
                ResumePicking();
            }
        }

        private void SubmitAndResume(ServaLabelPrintRequest request, Guid? printerID)
        {
            try
            {
                var service = PXGraph.CreateInstance<ServaLabelPrintService>();
                request.PrinterID = printerID;
                service.Submit(request);
                Basis.ReportInfo("Queued {0} label(s) to {1}.", request.LabelCount, request.PrinterName);
            }
            catch (Exception ex)
            {
                Basis.ReportError("The pick succeeded, but label printing failed: {0}", ex.Message);
            }
            finally
            {
                ResumePicking();
            }
        }

        public sealed class LabelCountState : WMSBase.EntityState<int?>
        {
            public const string Value = "SVLCNT";
            public override string Code => Value;
            protected override string StatePrompt => "Enter label count 0-99. Enter 0 to skip.";

            protected override int? GetByBarcode(string barcode) =>
                int.TryParse(barcode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    ? value
                    : (int?)null;

            protected override Validation Validate(int? value) =>
                value >= 0 && value <= ServaSOPickConstants.MaximumLabelCount
                    ? Validation.Ok
                    : Validation.Fail("Label count must be a whole number from 0 to 99.");

            protected override void Apply(int? value) => ContextFor(Basis).UsrServaLabelCount = value;
            protected override void ClearState() { }
            protected override void ReportMissing(string barcode) =>
                Basis.ReportError("Label count must be a whole number from 0 to 99.");
            protected override void ReportSuccess(int? value) { }
            protected override void SetNextState() => Get<Logic>().CreateRequest(ContextFor(Basis).UsrServaLabelCount);

            public class Logic : PickPackShip.ScanExtension
            {
                public virtual void CreateRequest(int? labelCount)
                {
                    try
                    {
                        var service = PXGraph.CreateInstance<ServaLabelPrintService>();
                        int count = labelCount.GetValueOrDefault();
                        ServaLabelPrintRequest request = service.SetLabelCount(Basis.RefNbr, count);

                        if (count == 0)
                        {
                            Basis.ReportInfo("Label printing skipped. Pick the next item.");
                            Basis.Get<ServaSOPickScanExtension>().ResumePicking();
                            return;
                        }

                        Basis.SetScanState<LabelQuantityState>();
                    }
                    catch (Exception ex)
                    {
                        Basis.ReportError("The pick succeeded, but the label request failed: {0}", ex.Message);
                        Basis.Get<ServaSOPickScanExtension>().ResumePicking();
                    }
                }
            }
        }

        public sealed class LabelQuantityState : WMSBase.EntityState<decimal?>
        {
            public const string Value = "SVLQTY";
            public override string Code => Value;
            protected override string StatePrompt => "Enter the quantity to print on each label.";

            protected override decimal? GetByBarcode(string barcode) =>
                decimal.TryParse(barcode, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
                    ? value
                    : (decimal?)null;

            protected override Validation Validate(decimal? value) =>
                value > 0m
                    ? Validation.Ok
                    : Validation.Fail("Label quantity must be greater than zero.");

            protected override void Apply(decimal? value) => ContextFor(Basis).UsrServaLabelQuantity = value;
            protected override void ClearState() { }
            protected override void ReportMissing(string barcode) =>
                Basis.ReportError("Label quantity must be a positive number.");
            protected override void ReportSuccess(decimal? value) { }
            protected override void SetNextState() => Get<Logic>().SaveQuantity(ContextFor(Basis).UsrServaLabelQuantity);

            public class Logic : PickPackShip.ScanExtension
            {
                public virtual void SaveQuantity(decimal? labelQuantity)
                {
                    try
                    {
                        PXGraph.CreateInstance<ServaLabelPrintService>()
                            .SetLabelQuantity(Basis.RefNbr, labelQuantity.GetValueOrDefault());
                        Basis.SetScanState<PrinterSelectionState>();
                    }
                    catch (Exception ex)
                    {
                        Basis.ReportError("The pick succeeded, but the label quantity could not be saved: {0}", ex.Message);
                        Basis.Get<ServaSOPickScanExtension>().ResumePicking();
                    }
                }
            }
        }

        public sealed class PrinterSelectionState : WMSBase.EntityState<Guid?>
        {
            public const string Value = "SVPRNT";
            public override string Code => Value;
            protected override string StatePrompt => "Tap Print Label for the default printer, or scan a printer name, DeviceHub ID, or GUID.";

            protected override Guid? GetByBarcode(string barcode) =>
                PXGraph.CreateInstance<ServaLabelPrintService>().ResolvePrinterID(barcode);
            protected override Validation Validate(Guid? value) =>
                value != null ? Validation.Ok : Validation.Fail("Printer not found. Scan its configured name, DeviceHub ID, or GUID.");
            protected override void Apply(Guid? value) => ContextFor(Basis).UsrServaPrinterOverrideID = value;
            protected override void ClearState() { }
            protected override void ReportMissing(string barcode) =>
                Basis.ReportError("Printer was not found. Scan its configured name, DeviceHub ID, or GUID.");
            protected override void ReportSuccess(Guid? value) { }
            protected override void SetNextState() =>
                Basis.Get<ServaSOPickScanExtension>().SubmitPendingAndResume(ContextFor(Basis).UsrServaPrinterOverrideID);
        }

        public sealed class SelectPrinterCommand : PickPackShip.ScanCommand
        {
            public override string Code => "PRINTER";
            public override string ButtonName => "ServaScanSelectPrinter";
            public override string DisplayName => "Print Label";
            protected override bool IsEnabled => true;
            protected override bool Process() => Basis.Get<ServaSOPickScanExtension>().PrintWithEffectivePrinter();
        }
    }

    public class ServaMandatoryPickQtyExtension : PickPackShip.ScanExtension<PickPackShip.QtySupport>
    {
        public static bool IsActive() => true;

        [PXOverride]
        public virtual bool get_IsMandatoryQtyInput(Func<bool> baseIsMandatoryQtyInput) =>
            baseIsMandatoryQtyInput()
            || Basis.CurrentMode is PickPackShip.PickMode
                && Basis.Remove != true
                && Basis.InventoryID != null
                && Basis.HeaderView.Current.PrevScanState != PickPackShip.QtySupport.QtyState.Value;
    }
}
