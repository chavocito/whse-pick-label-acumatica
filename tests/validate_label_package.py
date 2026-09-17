import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCES = sorted((ROOT / "src").glob("*.cs"))
DATABASE = ROOT / "database" / "SERVA_SO_PICK_FACADE_DB_20260911_01.sql"
REPORT = ROOT / "reports" / "SV619211.rpx"
PAGE = ROOT / "pages" / "SV" / "SV619210.aspx"
PAGE_CODEBEHIND = ROOT / "pages" / "SV" / "SV619210.aspx.cs"
MOBILE = ROOT / "mobile" / "serva-so-pick-facade.msd"
COMPILE_CHECK = ROOT / "tests" / "compile_against_acumatica.ps1"


def text(path):
    return path.read_text(encoding="utf-8")


def test_package_shape():
    required = [DATABASE, REPORT, PAGE, PAGE_CODEBEHIND, MOBILE, COMPILE_CHECK]
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
    assert "class PrinterSelectionState" in source
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
    assert "QuantityPerLabel" not in source
    assert "ContainerID" not in source
    assert "SHIP01" not in source


def test_print_contract():
    source = "\n".join(text(path) for path in SOURCES)
    assert 'ReportID = "SV619211"' in source
    assert "PickedQty = pickedQty" in source
    assert "OriginalRequestID = original.RequestID" in source
    assert "ReprintReason = reason.Trim()" in source
    assert "NumberOfCopies = 1" in source
    assert "for (int copy = 1; copy <= request.LabelCount" in source
    assert ".GetAwaiter().GetResult()" in source
    assert "if (!accepted)" in source
    assert "SubmittedStatus" in source and "FailedStatus" in source and "SkippedStatus" in source
    assert "The pick succeeded, but label printing failed" in source
    assert "No pending label request was found for printer selection" in source

    report = ET.parse(REPORT).getroot()
    assert report.findtext("./Parameters/ReportParameter/Name") == "RequestID"
    assert report.findtext("./Filters/FilterExp/DataField") == "ServaLabelPrintRequest.RequestID"
    assert report.findtext("./Filters/FilterExp/Value") == "@RequestID"
    assert report.findtext("./PageSettings/PaperKind") == "Custom"
    assert report.findtext("./PageSettings/PaperSize") == "384px, 288px"
    fields = {node.attrib["Name"] for node in report.findall("./Tables/ReportTable/Fields/ReportField")}
    assert {"RequestID", "ItemNumber", "PickedQty", "UOM", "LocationCD", "OrderNbr", "CountryOfOrigin"} <= fields
    report_text = text(REPORT)
    assert "QuantityPerLabel" not in report_text
    assert "ContainerID" not in report_text
    assert "ShipLocationCD" not in report_text


def test_database_migration():
    sql = text(DATABASE)
    assert "OBJECT_ID(N'dbo.ServaLabelPrintRequest'" in sql
    assert "ServaSOPickLease" not in sql
    for column in ("OriginalRequestID", "ReprintReason", "LastModifiedByID", "LastModifiedDateTime"):
        assert f"COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'{column}')" in sql
    assert "PRIMARY KEY CLUSTERED (CompanyID, RequestID)" in sql
    assert "DROP TABLE" not in sql.upper()


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
    assert 'Name="Reprint"' in page and 'Name="Retry"' in page
    assert "AutoSize" not in page
    for field in ("ShipmentNbr", "Status", "FromDate", "ToDate"):
        assert f'DataField="{field}"' in page
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
