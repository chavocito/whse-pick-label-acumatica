using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.CS;
using PX.Objects.IN;
using PX.Objects.SO;
using PX.SM;

namespace Serva.Acumatica.SoScannerLabels
{
    public class ServaLabelPrintService : PXGraph<ServaLabelPrintService>
    {
        public PXSelect<ServaLabelPrintRequest> Requests;

        public virtual Guid? GetDefaultPrinterID()
        {
            UserPreferences preferences = SelectFrom<UserPreferences>
                .Where<UserPreferences.userID.IsEqual<@P.AsGuid>>
                .View.ReadOnly.Select(this, Accessinfo.UserID);
            return preferences?.DefaultPrinterID;
        }

        public virtual ServaLabelPrintRequest GetRequest(int? requestID)
        {
            ServaLabelPrintRequest request = SelectFrom<ServaLabelPrintRequest>
                .Where<ServaLabelPrintRequest.requestID.IsEqual<@P.AsInt>>
                .View.Select(this, requestID);
            if (request == null)
                throw new PXException("Label request {0} was not found.", requestID);
            return request;
        }

        public virtual Guid? ResolvePrinterID(string value)
        {
            string barcode = value?.Trim();
            if (string.IsNullOrWhiteSpace(barcode))
                return null;

            foreach (SMPrinter printer in SelectFrom<SMPrinter>.View.ReadOnly.Select(this))
            {
                if (printer.IsActive == true
                    && (printer.PrinterID?.ToString().Equals(barcode, StringComparison.OrdinalIgnoreCase) == true
                        || string.Equals(printer.PrinterName, barcode, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(printer.DeviceHubID, barcode, StringComparison.OrdinalIgnoreCase)))
                    return printer.PrinterID;
            }
            return null;
        }

        public virtual ServaLabelPrintRequest CreatePickRequest(
            string shipmentNbr,
            int? lineNbr,
            int? locationID,
            decimal? pickedQty,
            string uom,
            string orderType,
            string orderNbr,
            int? inventoryID,
            int? siteID,
            string itemDescription,
            int? labelCount,
            Guid? printerID,
            string errorMessage = null)
        {
            if (labelCount != null)
                ValidateLabelCount(labelCount.Value);

            InventoryItem item = InventoryItem.PK.Find(this, inventoryID);
            INLocation location = INLocation.PK.Find(this, locationID);
            INSite site = INSite.PK.Find(this, siteID);
            var request = new ServaLabelPrintRequest
            {
                Source = ServaSOPickConstants.PickSource,
                ShipmentNbr = shipmentNbr,
                OrderType = orderType,
                OrderNbr = orderNbr,
                LineNbr = lineNbr,
                InventoryID = inventoryID,
                ItemNumber = item?.InventoryCD,
                ItemDescription = itemDescription ?? item?.Descr,
                SiteID = siteID,
                SiteName = site?.Descr ?? site?.SiteCD,
                LocationID = locationID,
                LocationCD = location?.LocationCD,
                UOM = uom,
                PickedQty = pickedQty,
                QuantityPerLabel = null,
                LabelCount = labelCount,
                CountryOfOrigin = item?.CountryOfOrigin,
                PrinterID = printerID,
                Status = !string.IsNullOrWhiteSpace(errorMessage)
                    ? ServaSOPickConstants.FailedStatus
                    : labelCount == 0
                        ? ServaSOPickConstants.SkippedStatus
                        : ServaSOPickConstants.PendingStatus,
                ErrorMessage = errorMessage
            };

            request = Requests.Insert(request);
            Persist();
            return request;
        }

        public virtual ServaLabelPrintRequest FindLatestPickRequest(string shipmentNbr, bool awaitingLabelCount)
        {
            var rows = SelectFrom<ServaLabelPrintRequest>
                .Where<ServaLabelPrintRequest.shipmentNbr.IsEqual<@P.AsString>
                    .And<ServaLabelPrintRequest.source.IsEqual<@P.AsString>>
                    .And<ServaLabelPrintRequest.createdByID.IsEqual<@P.AsGuid>>>
                .OrderBy<ServaLabelPrintRequest.requestID.Desc>
                .View.ReadOnly.Select(this, shipmentNbr, ServaSOPickConstants.PickSource, Accessinfo.UserID);

            foreach (ServaLabelPrintRequest request in rows)
            {
                if (request.Status == ServaSOPickConstants.PendingStatus
                    && (awaitingLabelCount
                        ? request.LabelCount == null
                        : request.LabelCount > 0 && request.QuantityPerLabel > 0m))
                    return request;
            }

            return null;
        }

        public virtual ServaLabelPrintRequest GetLatestPickRequest(string shipmentNbr, bool awaitingLabelCount) =>
            FindLatestPickRequest(shipmentNbr, awaitingLabelCount)
            ?? throw new PXException(
                awaitingLabelCount
                    ? "No pending pick was found for label count entry."
                    : "No pending label request was found for printer selection.");

        public virtual ServaLabelPrintRequest SetLabelCount(string shipmentNbr, int labelCount)
        {
            ValidateLabelCount(labelCount);
            ServaLabelPrintRequest request = GetLatestPickRequest(shipmentNbr, awaitingLabelCount: true);
            request.LabelCount = labelCount;
            request.Status = labelCount == 0
                ? ServaSOPickConstants.SkippedStatus
                : ServaSOPickConstants.PendingStatus;
            Requests.Update(request);
            Persist();
            return request;
        }

        public virtual ServaLabelPrintRequest SetLabelQuantity(string shipmentNbr, decimal labelQuantity)
        {
            ValidateLabelQuantity(labelQuantity);
            var rows = SelectFrom<ServaLabelPrintRequest>
                .Where<ServaLabelPrintRequest.shipmentNbr.IsEqual<@P.AsString>
                    .And<ServaLabelPrintRequest.source.IsEqual<@P.AsString>>
                    .And<ServaLabelPrintRequest.createdByID.IsEqual<@P.AsGuid>>>
                .OrderBy<ServaLabelPrintRequest.requestID.Desc>
                .View.ReadOnly.Select(this, shipmentNbr, ServaSOPickConstants.PickSource, Accessinfo.UserID);

            ServaLabelPrintRequest request = null;
            foreach (ServaLabelPrintRequest row in rows)
            {
                if (row.Status == ServaSOPickConstants.PendingStatus
                    && row.LabelCount > 0
                    && row.QuantityPerLabel == null)
                {
                    request = row;
                    break;
                }
            }

            if (request == null)
                throw new PXException("No pending label request was found for label quantity entry.");

            request.QuantityPerLabel = labelQuantity;
            Requests.Update(request);
            Persist();
            return request;
        }

        public virtual ServaLabelPrintRequest CreateDerivedRequest(
            ServaLabelPrintRequest original,
            string source,
            int labelCount,
            string reason,
            Guid? printerID)
        {
            if (original == null)
                throw new PXException("Select an original print request.");
            ValidateLabelCount(labelCount);
            if (labelCount == 0)
                throw new PXException("Retry and reprint label count must be between 1 and 99.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new PXException("Enter a reason for the retry or reprint.");

            var request = new ServaLabelPrintRequest
            {
                Source = source,
                OriginalRequestID = original.RequestID,
                ShipmentNbr = original.ShipmentNbr,
                OrderType = original.OrderType,
                OrderNbr = original.OrderNbr,
                LineNbr = original.LineNbr,
                InventoryID = original.InventoryID,
                ItemNumber = original.ItemNumber,
                ItemDescription = original.ItemDescription,
                SiteID = original.SiteID,
                SiteName = original.SiteName,
                LocationID = original.LocationID,
                LocationCD = original.LocationCD,
                UOM = original.UOM,
                PickedQty = original.PickedQty,
                QuantityPerLabel = original.QuantityPerLabel ?? original.PickedQty,
                LabelCount = labelCount,
                CountryOfOrigin = original.CountryOfOrigin,
                PrinterID = printerID ?? original.PrinterID,
                ReprintReason = reason.Trim(),
                Status = ServaSOPickConstants.PendingStatus
            };

            request = Requests.Insert(request);
            Persist();
            return request;
        }

        public virtual void Submit(ServaLabelPrintRequest request)
        {
            if (request == null || request.LabelCount.GetValueOrDefault() == 0)
                return;

            try
            {
                ValidateLabelQuantity(request.QuantityPerLabel.GetValueOrDefault());
                SMPrinter printer = ResolvePrinter(request.PrinterID);
                request.PrinterID = printer.PrinterID;
                request.PrinterName = printer.PrinterName;

                if (string.IsNullOrWhiteSpace(request.CountryOfOrigin))
                    PXTrace.WriteWarning("Serva label request {0} has no country of origin.", request.RequestID);

                var parameters = new Dictionary<string, string>
                {
                    { "RequestID", request.RequestID.ToString() }
                };

                for (int copy = 1; copy <= request.LabelCount.GetValueOrDefault(); copy++)
                {
                    var printSettings = new PrintSettings
                    {
                        PrinterID = printer.PrinterID,
                        NumberOfCopies = 1,
                        PrintWithDeviceHub = true,
                        DefinePrinterManually = true
                    };

                    bool accepted = PXGraph.CreateInstance<SMPrintJobMaint>().CreatePrintJob(
                        printSettings,
                        ServaSOPickConstants.ReportID,
                        parameters,
                        string.Format("Serva Item Pick Tag {0} ({1}/{2})", request.RequestID, copy, request.LabelCount),
                        CancellationToken.None).GetAwaiter().GetResult();
                    if (!accepted)
                        throw new PXException(
                            "DeviceHub did not accept label {0} of {1}. Verify report {2}, the selected printer, and DeviceHub connectivity.",
                            copy,
                            request.LabelCount,
                            ServaSOPickConstants.ReportID);
                }

                request.Status = ServaSOPickConstants.SubmittedStatus;
                request.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                request.Status = ServaSOPickConstants.FailedStatus;
                request.ErrorMessage = GetErrorMessage(ex);
                PXTrace.WriteError(ex);
                throw;
            }
            finally
            {
                Requests.Update(request);
                Persist();
            }
        }

        protected virtual SMPrinter ResolvePrinter(Guid? printerID)
        {
            if (!PXAccess.FeatureInstalled<FeaturesSet.deviceHub>())
                throw new PXException("DeviceHub is not enabled. Enable it before printing Serva item tags.");

            Guid? effectivePrinterID = printerID ?? GetDefaultPrinterID();
            if (effectivePrinterID == null)
                throw new PXException("No printer is configured. Set Default Printer in SM203010 or use the PRINTER command to select an override.");

            SMPrinter printer = SelectFrom<SMPrinter>
                .Where<SMPrinter.printerID.IsEqual<@P.AsGuid>>
                .View.ReadOnly.Select(this, effectivePrinterID);
            if (printer == null)
                throw new PXException("The selected DeviceHub printer was not found. Run Update Printer List in SM206510 and select it again.");
            if (printer.IsActive != true)
                throw new PXException("The selected DeviceHub printer is inactive. Activate it in SM206510 or select another printer.");
            return printer;
        }

        private static string GetErrorMessage(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                AggregateException flattened = aggregate.Flatten();
                exception = flattened.InnerExceptions.Count == 0 ? aggregate : flattened.InnerExceptions[0];
            }
            while (exception.InnerException != null)
                exception = exception.InnerException;
            return exception.Message;
        }

        public static void ValidateLabelCount(int labelCount)
        {
            if (labelCount < 0 || labelCount > ServaSOPickConstants.MaximumLabelCount)
                throw new PXException("Label count must be a whole number from 0 to 99.");
        }

        public static void ValidateLabelQuantity(decimal labelQuantity)
        {
            if (labelQuantity <= 0m)
                throw new PXException("Label quantity must be greater than zero.");
        }
    }

    public class ServaLabelHistoryEntry : PXGraph<ServaLabelHistoryEntry>
    {
        public PXFilter<ServaLabelHistoryFilter> Filter;
        public PXSelect<ServaLabelPrintRequest> Requests;

        protected virtual IEnumerable requests()
        {
            ServaLabelHistoryFilter filter = Filter.Current;
            var rows = SelectFrom<ServaLabelPrintRequest>
                .OrderBy<ServaLabelPrintRequest.requestID.Desc>
                .View.ReadOnly.Select(this);
            foreach (ServaLabelPrintRequest row in rows)
            {
                if (!string.IsNullOrWhiteSpace(filter?.ShipmentNbr) && row.ShipmentNbr != filter.ShipmentNbr)
                    continue;
                if (!string.IsNullOrWhiteSpace(filter?.Status) && row.Status != filter.Status)
                    continue;
                if (filter?.FromDate != null && row.CreatedDateTime < filter.FromDate)
                    continue;
                if (filter?.ToDate != null && row.CreatedDateTime >= filter.ToDate.Value.AddDays(1))
                    continue;
                yield return row;
            }
        }

        public PXAction<ServaLabelHistoryFilter> Reprint;
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Reprint")]
        protected virtual IEnumerable reprint(PXAdapter adapter)
        {
            SubmitDerived(ServaSOPickConstants.ReprintSource, requireFailed: false);
            return adapter.Get();
        }

        public PXAction<ServaLabelHistoryFilter> Retry;
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Retry")]
        protected virtual IEnumerable retry(PXAdapter adapter)
        {
            SubmitDerived(ServaSOPickConstants.RetrySource, requireFailed: true);
            return adapter.Get();
        }

        protected virtual void SubmitDerived(string source, bool requireFailed)
        {
            ServaLabelHistoryFilter filter = Filter.Current;
            ServaLabelPrintRequest original = Requests.Current;
            if (original == null)
                throw new PXException("Select a print-history row.");
            if (requireFailed && original.Status != ServaSOPickConstants.FailedStatus)
                throw new PXException("Only a failed print request can be retried. Use Reprint for a submitted request.");

            int count = filter?.LabelCount ?? 0;
            var service = PXGraph.CreateInstance<ServaLabelPrintService>();
            ServaLabelPrintRequest request = service.CreateDerivedRequest(
                original,
                source,
                count,
                filter?.Reason,
                filter?.PrinterID);
            service.Submit(request);
            Filter.Cache.Clear();
            Requests.View.RequestRefresh();
        }
    }
}
