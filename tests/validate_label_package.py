import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCES = sorted((ROOT / "src").glob("*.cs"))
DATABASE = ROOT / "database" / "SERVA_SO_PICK_FACADE_DB_20260911_01.sql"
DATABASE_PATCH = ROOT / "database" / "SERVA_SO_PICK_FACADE_DB_20260911_02.sql"
ZPL_TEMPLATE = ROOT / "sv619210.zpl"
PAGE = ROOT / "pages" / "SV" / "SV619210.aspx"
PAGE_CODEBEHIND = ROOT / "pages" / "SV" / "SV619210.aspx.cs"
MOBILE = ROOT / "mobile" / "serva-so-pick-facade.msd"
COMPILE_CHECK = ROOT / "tests" / "compile_against_acumatica.ps1"


def text(path):
    return path.read_text(encoding="utf-8")


def test_package_shape():
    required = [DATABASE, DATABASE_PATCH, ZPL_TEMPLATE, PAGE, PAGE_CODEBEHIND, MOBILE, COMPILE_CHECK]
    assert all(path.exists() for path in required), "a required package artifact is missing"
    assert len(SOURCES) == 3, f"expected three focused C# files, found {len(SOURCES)}"
    removed = [
        ROOT / "src" / "ServaSoScannerLabels.cs",
        ROOT / "reports" / "SV619201.rpx",
        ROOT / "pages" / "SV" / "SV619200.aspx",
        ROOT / "mobile" / "serva-label-mobile.msd",
        ROOT / "legacyentry.cs",
        ROOT / "src" / "ServaSOPickResolver.cs",
    ]
    assert not any(path.exists() for path in removed), "an obsolete runtime artifact remains"


def test_shipment_first_scanner_contract():
    source = "\n".join(text(path) for path in SOURCES)
    assert "class DocumentTypeState" not in source
    assert "UsrServaDocumentType" not in source
    assert "ServaSOPickResolver" not in source
    assert "PickMode.ShipmentState" not in source
    assert "CreateShipmentExtension" not in source
    assert "CreateShipmentArgs" not in source
    assert "ServaSOPickLease" not in source
    assert "PickPackShip.PickMode.ConfirmState" in source
    assert "PerformConfirmation.ByOverride" in source
    assert "class ServaMandatoryPickQtyExtension" in source
    assert "get_IsMandatoryQtyInput" in source
    assert "PickPackShip.QtySupport.QtyState.Value" in source
    assert "basis.DispatchNext()" in source
    assert "class LabelCountState" in source
    assert "class LabelQuantityState" in source
    assert "class PrinterSelectionState" in source
    assert "Enter Y to print to the default printer" in source
    assert 'string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase)' in source
    assert 'string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase)' in source
    assert "return service.GetDefaultPrinterID()" in source
    assert "UsrServaPendingRequestID" not in source
    assert "UsrServaInventoryID" not in source
    assert ".View.ReadOnly.Select(basis.Graph, split.ShipmentNbr, split.LineNbr)" in source
    assert "CreatePickRequest(" in source
    assert "FindLatestPickRequest" in source
    assert "Basis.Clear<PickPackShip.LocationState>()" in source
    assert "The committed shipment line context was not captured" in source
    assert "The committed shipment line could not be found" not in source
    assert "ResolvePrinterID" in source
    assert "Print Label" in source
    assert "AskExt()" not in source
    assert "PrinterPrompt" not in source
    assert "printer.PrinterName" in source and "printer.DeviceHubID" in source
    assert "class SelectPrinterCommand" in source
    assert 'Code => "PRINTER"' in source
    assert "public PXAction<ScanHeader> ServaSelectPrinter" in source
    assert 'ButtonName => "ServaScanSelectPrinter"' in source
    assert "QuantityPerLabel" in source
    assert "SetLabelQuantity" in source
    assert "Label quantity must be greater than zero" in source
    assert "ContainerID" not in source
    assert "SHIP01" not in source


def test_print_contract():
    source = "\n".join(text(path) for path in SOURCES)
    assert "PickedQty = pickedQty" in source
    assert "QuantityPerLabel = original.QuantityPerLabel ?? original.PickedQty" in source
    assert "OriginalRequestID = original.RequestID" in source
    assert "ReprintReason = reason.Trim()" in source
    assert "[nameof(IPrintable.NumberOfCopies)] = 1" in source
    assert "for (int copy = 1; copy <= request.LabelCount" in source
    assert "BuildSalesOrderLabelZpl" in source
    assert "UploadZplFile" in source
    assert "Encoding.UTF8.GetBytes" in source
    assert '"FILEID", zplFile.UID.ToString()' in source
    assert "UploadFileMaintenance" in source
    assert "FileExistsAction.CreateVersion" in source
    assert "ZplFileID = zplFile.UID" in source
    assert "ZplFileName = zplFile.Name" in source
    assert "CreatePrintJobForRawFile(" in source
    assert "PXView.Dummy.For<ServaLabelPrintRequest>(this)" in source
    assert "delegate { return printer.PrinterID; }" in source
    assert "Serva SO Label ZPL" in source
    assert "raw ZPL sales-order label" in source
    assert "SODate = order?.OrderDate" in source
    assert "CompanyName = organization?.OrganizationName" in source
    assert "CompanyCountry = branchAddress?.CountryID" in source
    assert "FormatQuantity(request.QuantityPerLabel)" in source
    assert "public PXAction<ServaLabelHistoryFilter> DownloadZpl" in source
    assert "PXRedirectToFileException(file, true)" in source
    assert ".GetAwaiter().GetResult()" in source
    assert "if (!accepted)" in source
    assert "SubmittedStatus" in source and "FailedStatus" in source and "SkippedStatus" in source
    assert "The pick succeeded, but label printing failed" in source
    assert "No pending label request was found for printer selection" in source

    for command in ("^PW812", "^LL609", "^FO60,130^BQ,2,7", "^FO740,45^A0N,32,32", "^FO650,450^A0N,28,28"):
        assert command in source and command in text(ZPL_TEMPLATE)


def test_database_migration():
    sql = text(DATABASE)
    patch = text(DATABASE_PATCH)
    assert "OBJECT_ID(N'dbo.ServaLabelPrintRequest'" in sql
    assert "ServaSOPickLease" not in sql
    for column in ("OriginalRequestID", "ReprintReason", "QuantityPerLabel", "LastModifiedByID", "LastModifiedDateTime", "SODate", "CompanyName", "CompanyCountry", "ZplFileID", "ZplFileName"):
        assert f"COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'{column}')" in sql
    assert "QuantityPerLabel decimal(25,6)" in sql
    assert "PRIMARY KEY CLUSTERED (CompanyID, RequestID)" in sql
    assert "DROP TABLE" not in sql.upper()
    assert "COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'QuantityPerLabel')" in patch
    assert "ADD QuantityPerLabel decimal(25,6) NULL" in patch
    assert "DROP TABLE" not in patch.upper()


def test_web_and_mobile_contract():
    page = text(PAGE)
    page_codebehind = text(PAGE_CODEBEHIND)
    readme = text(ROOT / "README.md")
    assert "ServaLabelHistoryEntry" in page
    assert 'CodeFile="SV619210.aspx.cs"' in page
    assert 'Inherits="Page_SV619210"' in page
    assert "class Page_SV619210 : PXPage" in page_codebehind
    assert 'PrimaryView="Filter"' in page
    assert 'DataMember="Requests"' in page
    assert not re.search(r'<px:PXGrid\b[^>]*\bDataMember=', page)
    assert re.search(r'<px:PXGridLevel\b[^>]*\bDataMember="Requests"', page)
    assert 'Name="Reprint"' in page and 'Name="Retry"' in page and 'Name="DownloadZpl"' in page
    assert "AutoSize" not in page
    for field in ("ShipmentNbr", "Status", "FromDate", "ToDate"):
        assert f'DataField="{field}"' in page
    assert '<px:PXSelector ID="edShipmentNbr"' in page
    assert '<px:PXDropDown ID="edStatus"' in page
    assert 'DataField="QuantityPerLabel"' in page
    assert 'DataField="ZplFileName"' in page
    source = "\n".join(text(path) for path in SOURCES)
    assert "typeof(Search<SOShipment.shipmentNbr>)" in source
    assert "PXStringList" in source
    for field in ("OriginalRequestID", "OrderNbr", "LineNbr", "SiteID", "ReprintReason"):
        assert f'DataField="{field}"' not in page
    assert "Graph Name: `ServaLabelHistoryScreen`" in readme
    assert "public class ServaLabelHistoryScreen : ServaLabelHistoryEntry" in readme
    assert 'TypeName="Serva.Acumatica.SoScannerLabels.ServaLabelHistoryScreen"' in readme
    assert "Pages\\SV\\SV619210.aspx" in readme
    assert "must not exist under both **Screens** and **Files**" in readme
    assert "Shipment-Line Capture" in readme

    mobile = text(MOBILE)
    assert "update screen SO302020" in mobile
    assert 'add recordAction "ServaSelectPrinter"' in mobile
    assert 'recordActionLink "ServaSelectPrinter"' not in mobile
    assert 'update layout "Line1"' not in mobile
    assert 'displayName = "Print Label"' in mobile
    assert "redirectToDialog" not in mobile
    assert "add dialog" not in mobile
    assert "add screen SV619210" not in mobile
    assert "update sitemap" not in mobile


def test_compile_harness():
    script = text(COMPILE_CHECK)
    assert 'StartsWith("26.1")' in script
    assert 'Get-ChildItem -LiteralPath $sourceDirectory -Filter "*.cs"' in script
    assert '"PX.BarcodeProcessing.dll"' in script
    assert not re.search(r"ServaSoScannerLabels\.cs", script)


if __name__ == "__main__":
    test_package_shape()
    test_shipment_first_scanner_contract()
    test_print_contract()
    test_database_migration()
    test_web_and_mobile_contract()
    test_compile_harness()
    print("Shipment picking label package validation passed")
