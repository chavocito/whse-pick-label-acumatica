using System;
using PX.BarcodeProcessing;
using PX.Data;
using PX.Data.BQL;
using PX.Objects.IN;
using PX.Objects.IN.WMS;
using PX.SM;

namespace Serva.Acumatica.SoScannerLabels
{
    public static class ServaSOPickConstants
    {
        public const string PickSource = "PICK";
        public const string RetrySource = "RETRY";
        public const string ReprintSource = "REPRINT";
        public const string SkippedStatus = "SKIPPED";
        public const string PendingStatus = "PENDING";
        public const string SubmittedStatus = "SUBMITTED";
        public const string FailedStatus = "FAILED";
        public const string ReportID = "SV619211";
        public const int MaximumLabelCount = 99;
    }

    [Serializable]
    [PXCacheName("Serva Label Print Request")]
    public class ServaLabelPrintRequest : PXBqlTable, IBqlTable
    {
        [PXDBIdentity(IsKey = true)]
        [PXUIField(DisplayName = "Request ID")]
        public virtual int? RequestID { get; set; }
        public abstract class requestID : BqlInt.Field<requestID> { }

        [PXDBString(12, IsUnicode = false)]
        [PXUIField(DisplayName = "Source")]
        public virtual string Source { get; set; }
        public abstract class source : BqlString.Field<source> { }

        [PXDBInt]
        [PXUIField(DisplayName = "Original Request ID")]
        public virtual int? OriginalRequestID { get; set; }
        public abstract class originalRequestID : BqlInt.Field<originalRequestID> { }

        [PXDBString(15, IsUnicode = false)]
        [PXUIField(DisplayName = "Shipment Nbr.")]
        public virtual string ShipmentNbr { get; set; }
        public abstract class shipmentNbr : BqlString.Field<shipmentNbr> { }

        [PXDBString(2, IsUnicode = false)]
        [PXUIField(DisplayName = "Order Type")]
        public virtual string OrderType { get; set; }
        public abstract class orderType : BqlString.Field<orderType> { }

        [PXDBString(15, IsUnicode = false)]
        [PXUIField(DisplayName = "Order Nbr.")]
        public virtual string OrderNbr { get; set; }
        public abstract class orderNbr : BqlString.Field<orderNbr> { }

        [PXDBInt]
        [PXUIField(DisplayName = "Shipment Line Nbr.")]
        public virtual int? LineNbr { get; set; }
        public abstract class lineNbr : BqlInt.Field<lineNbr> { }

        [Inventory]
        [PXUIField(DisplayName = "Inventory ID")]
        public virtual int? InventoryID { get; set; }
        public abstract class inventoryID : BqlInt.Field<inventoryID> { }

        [PXDBString(30, IsUnicode = true)]
        [PXUIField(DisplayName = "Item Number")]
        public virtual string ItemNumber { get; set; }
        public abstract class itemNumber : BqlString.Field<itemNumber> { }

        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Description")]
        public virtual string ItemDescription { get; set; }
        public abstract class itemDescription : BqlString.Field<itemDescription> { }

        [Site]
        [PXUIField(DisplayName = "Warehouse")]
        public virtual int? SiteID { get; set; }
        public abstract class siteID : BqlInt.Field<siteID> { }

        [PXDBString(60, IsUnicode = true)]
        [PXUIField(DisplayName = "Warehouse Name")]
        public virtual string SiteName { get; set; }
        public abstract class siteName : BqlString.Field<siteName> { }

        [Location(typeof(siteID))]
        [PXUIField(DisplayName = "Location")]
        public virtual int? LocationID { get; set; }
        public abstract class locationID : BqlInt.Field<locationID> { }

        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Location Code")]
        public virtual string LocationCD { get; set; }
        public abstract class locationCD : BqlString.Field<locationCD> { }

        [PXDBString(6, IsUnicode = false)]
        [PXUIField(DisplayName = "UOM")]
        public virtual string UOM { get; set; }
        public abstract class uOM : BqlString.Field<uOM> { }

        [PXDBDecimal]
        [PXUIField(DisplayName = "Picked Qty.")]
        public virtual decimal? PickedQty { get; set; }
        public abstract class pickedQty : BqlDecimal.Field<pickedQty> { }

        [PXDBInt]
        [PXUIField(DisplayName = "Label Count")]
        public virtual int? LabelCount { get; set; }
        public abstract class labelCount : BqlInt.Field<labelCount> { }

        [PXDBString(2, IsUnicode = false)]
        [PXUIField(DisplayName = "COO")]
        public virtual string CountryOfOrigin { get; set; }
        public abstract class countryOfOrigin : BqlString.Field<countryOfOrigin> { }

        [PXDBGuid]
        [PXUIField(DisplayName = "Printer ID", Visible = false)]
        public virtual Guid? PrinterID { get; set; }
        public abstract class printerID : BqlGuid.Field<printerID> { }

        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Printer")]
        public virtual string PrinterName { get; set; }
        public abstract class printerName : BqlString.Field<printerName> { }

        [PXDBString(12, IsUnicode = false)]
        [PXUIField(DisplayName = "Status")]
        public virtual string Status { get; set; }
        public abstract class status : BqlString.Field<status> { }

        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Reprint Reason")]
        public virtual string ReprintReason { get; set; }
        public abstract class reprintReason : BqlString.Field<reprintReason> { }

        [PXDBString(2048, IsUnicode = true)]
        [PXUIField(DisplayName = "Error")]
        public virtual string ErrorMessage { get; set; }
        public abstract class errorMessage : BqlString.Field<errorMessage> { }

        [PXDBCreatedByID]
        public virtual Guid? CreatedByID { get; set; }
        public abstract class createdByID : BqlGuid.Field<createdByID> { }

        [PXDBCreatedDateTime]
        [PXUIField(DisplayName = "Created At")]
        public virtual DateTime? CreatedDateTime { get; set; }
        public abstract class createdDateTime : BqlDateTime.Field<createdDateTime> { }

        [PXDBLastModifiedByID]
        public virtual Guid? LastModifiedByID { get; set; }
        public abstract class lastModifiedByID : BqlGuid.Field<lastModifiedByID> { }

        [PXDBLastModifiedDateTime]
        public virtual DateTime? LastModifiedDateTime { get; set; }
        public abstract class lastModifiedDateTime : BqlDateTime.Field<lastModifiedDateTime> { }

        [PXDBTimestamp]
        public virtual byte[] Tstamp { get; set; }
        public abstract class tstamp : BqlByteArray.Field<tstamp> { }
    }

    public class ServaWMSScanHeaderExt : PXCacheExtension<WMSScanHeader, ScanHeader>
    {
        [PXGuid]
        public virtual Guid? UsrServaPrinterOverrideID { get; set; }
        public abstract class usrServaPrinterOverrideID : BqlGuid.Field<usrServaPrinterOverrideID> { }

        [PXInt]
        public virtual int? UsrServaLabelCount { get; set; }
        public abstract class usrServaLabelCount : BqlInt.Field<usrServaLabelCount> { }
    }

    [Serializable]
    [PXCacheName("Serva Label History Filter")]
    public class ServaLabelHistoryFilter : PXBqlTable, IBqlTable
    {
        [PXString(15, IsUnicode = false)]
        [PXUIField(DisplayName = "Shipment Nbr.")]
        public virtual string ShipmentNbr { get; set; }
        public abstract class shipmentNbr : BqlString.Field<shipmentNbr> { }

        [PXString(12, IsUnicode = false)]
        [PXUIField(DisplayName = "Status")]
        public virtual string Status { get; set; }
        public abstract class status : BqlString.Field<status> { }

        [PXDate]
        [PXUIField(DisplayName = "From Date")]
        public virtual DateTime? FromDate { get; set; }
        public abstract class fromDate : BqlDateTime.Field<fromDate> { }

        [PXDate]
        [PXUIField(DisplayName = "To Date")]
        public virtual DateTime? ToDate { get; set; }
        public abstract class toDate : BqlDateTime.Field<toDate> { }

        [PXInt]
        [PXDefault(1)]
        [PXUIField(DisplayName = "Label Count", Required = true)]
        public virtual int? LabelCount { get; set; }
        public abstract class labelCount : BqlInt.Field<labelCount> { }

        [PXString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Reason", Required = true)]
        public virtual string Reason { get; set; }
        public abstract class reason : BqlString.Field<reason> { }

        [PXPrinterSelector]
        [PXUIField(DisplayName = "Printer Override")]
        public virtual Guid? PrinterID { get; set; }
        public abstract class printerID : BqlGuid.Field<printerID> { }
    }
}
