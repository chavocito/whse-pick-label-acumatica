using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.CS;
using PX.Objects.CR;
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
            SOOrder order = SelectFrom<SOOrder>
                .Where<SOOrder.orderType.IsEqual<@P.AsString>
                    .And<SOOrder.orderNbr.IsEqual<@P.AsString>>>
                .View.ReadOnly.Select(this, orderType, orderNbr);
            PX.Objects.GL.Branch branch = SelectFrom<PX.Objects.GL.Branch>
                .Where<PX.Objects.GL.Branch.branchID.IsEqual<@P.AsInt>>
                .View.ReadOnly.Select(this, order?.BranchID);
            PX.Objects.GL.DAC.Organization organization = SelectFrom<PX.Objects.GL.DAC.Organization>
                .Where<PX.Objects.GL.DAC.Organization.organizationID.IsEqual<@P.AsInt>>
                .View.ReadOnly.Select(this, branch?.OrganizationID);
            BAccount branchAccount = BAccount.PK.Find(this, branch?.BAccountID);
            Address branchAddress = Address.PK.Find(this, branchAccount?.DefAddressID);
            var request = new ServaLabelPrintRequest
            {
                Source = ServaSOPickConstants.PickSource,
                ShipmentNbr = shipmentNbr,
                OrderType = orderType,
                OrderNbr = orderNbr,
                SODate = order?.OrderDate,
                CompanyName = organization?.OrganizationName,
                CompanyCountry = branchAddress?.CountryID,
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
                SODate = original.SODate,
                CompanyName = original.CompanyName,
                CompanyCountry = original.CompanyCountry,
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
                ZplFileID = original.ZplFileID,
                ZplFileName = original.ZplFileName,
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

                if (string.IsNullOrWhiteSpace(request.CompanyCountry))
                    PXTrace.WriteWarning("Serva label request {0} has no branch/company country.", request.RequestID);

                for (int copy = 1; copy <= request.LabelCount.GetValueOrDefault(); copy++)
                {
                    PX.SM.FileInfo zplFile = UploadZplFile(request, copy);
                    request.ZplFileID = zplFile.UID;
                    request.ZplFileName = zplFile.Name;
                    var parameters = new Dictionary<string, string>
                    {
                        { "FILEID", zplFile.UID.ToString() }
                    };

                    var adapter = new PXAdapter(PXView.Dummy.For<ServaLabelPrintRequest>(this))
                    {
                        MassProcess = true,
                        Arguments =
                        {
                            [nameof(IPrintable.PrintWithDeviceHub)] = true,
                            [nameof(IPrintable.DefinePrinterManually)] = true,
                            [nameof(IPrintable.PrinterID)] = printer.PrinterID,
                            [nameof(IPrintable.NumberOfCopies)] = 1
                        }
                    };

                    bool accepted = SMPrintJobMaint.CreatePrintJobForRawFile(
                        adapter,
                        delegate { return printer.PrinterID; },
                        "Serva",
                        null,
                        Accessinfo.BranchID,
                        parameters,
                        string.Format("Serva SO Label ZPL {0} ({1}/{2})", request.RequestID, copy, request.LabelCount),
                        CancellationToken.None).GetAwaiter().GetResult();
                    if (!accepted)
                        throw new PXException(
                            "DeviceHub did not accept raw ZPL sales-order label {0} of {1}. Verify the selected printer is configured for raw mode and DeviceHub is connected.",
                            copy,
                            request.LabelCount);
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

        protected virtual PX.SM.FileInfo UploadZplFile(ServaLabelPrintRequest request, int copy)
        {
            byte[] labelBytes = Encoding.UTF8.GetBytes(BuildSalesOrderLabelZpl(request));
            string filename = string.Format(
                CultureInfo.InvariantCulture,
                "serva-so-label-{0}-{1}.zpl",
                request.RequestID,
                copy);
            var file = new PX.SM.FileInfo(filename, null, labelBytes);
            UploadFileMaintenance upload = PXGraph.CreateInstance<UploadFileMaintenance>();
            if (!upload.SaveFile(file, FileExistsAction.CreateVersion) || file.UID == null)
                throw new PXException("The raw ZPL label file could not be saved for DeviceHub printing.");
            return file;
        }

        protected virtual string BuildSalesOrderLabelZpl(ServaLabelPrintRequest request)
        {
            string item = ZplEscape(Clean(request.ItemNumber));
            string uom = ZplEscape(Clean(request.UOM));
            string quantity = ZplEscape(FormatQuantity(request.QuantityPerLabel));
            string company = ZplEscape(Clean(request.CompanyName));
            string description = ZplEscape(Clean(request.ItemDescription));
            string country = ZplEscape(Clean(request.CompanyCountry));
            string orderDate = ZplEscape(request.SODate?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) ?? string.Empty);
            string location = ZplEscape(Clean(request.LocationCD));
            string orderNbr = ZplEscape(Clean(request.OrderNbr));

            var zpl = new StringBuilder();
            zpl.AppendLine("^XA");
            zpl.AppendLine("^PW812");
            zpl.AppendLine("^LL609");
            zpl.AppendLine("^CI28");
            zpl.AppendLine("^FO60,50^A0N,25,25^FDSERVA^FS");
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO280,35^A0N,55,55^FH^FD{0}^FS\r\n", item);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO700,50^A0N,25,25^FH^FD{0}^FS\r\n", uom);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO740,45^A0N,32,32^FH^FD{0}^FS\r\n", quantity);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO60,130^BQ,2,7^FH^FDLA,{0}^FS\r\n", item);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO280,220^A0N,28,28^FH^FD{0}^FS\r\n", company);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO200,320^A0N,28,28^FB412,2,5,C^FH^FD{0}\\&^FS\r\n", description);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO70,450^A0N,24,24^FH^FDCOO: {0}^FS\r\n", country);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO70,500^A0N,24,24^FH^FD{0}^FS\r\n", orderDate);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO350,450^A0N,28,28^FH^FD{0}^FS\r\n", location);
            zpl.AppendFormat(CultureInfo.InvariantCulture, "^FO650,450^A0N,28,28^FH^FDSO# {0}^FS\r\n", orderNbr);
            zpl.AppendLine("^XZ");
            return zpl.ToString();
        }

        private static string FormatQuantity(decimal? value) =>
            value?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var result = new StringBuilder(value.Trim().Length);
            foreach (char c in value.Trim())
                result.Append(c == '\r' || c == '\n' || c == '\t' ? ' ' : char.IsControl(c) ? '?' : c);
            return result.ToString();
        }

        private static string ZplEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value
                .Replace("_", "_5F")
                .Replace("^", "_5E")
                .Replace("~", "_7E")
                .Replace("\\", "_5C");
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

        public PXAction<ServaLabelHistoryFilter> DownloadZpl;
        [PXButton]
        [PXUIField(DisplayName = "Download ZPL", MapEnableRights = PXCacheRights.Select, MapViewRights = PXCacheRights.Select)]
        protected virtual IEnumerable downloadZpl(PXAdapter adapter)
        {
            ServaLabelPrintRequest request = Requests.Current;
            if (request == null)
                throw new PXException("Select a print-history row.");
            if (request.ZplFileID == null)
                throw new PXException("No generated ZPL file is recorded for the selected print request.");

            PX.SM.FileInfo file = PXGraph.CreateInstance<UploadFileMaintenance>().GetFile(request.ZplFileID.Value);
            if (file == null)
                throw new PXException("The generated ZPL file could not be found.");
            throw new PXRedirectToFileException(file, true);
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
