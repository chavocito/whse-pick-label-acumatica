# Serva Shipment Picking Labels

Acumatica 2026 R1 customization for native shipment picking in `SO302020` with a label-count and DeviceHub print step after each successful pick.

## Operator Flow

```text
Scan Shipment number
  -> Native location scan
  -> Native item and lot/serial scan
  -> Enter native pick quantity
  -> Native pick commits
  -> Enter label count 0-99
     -> 0: record Skipped and continue picking
     -> 1-99: enter the quantity to print on each label
  -> Use the default/session printer, or scan another configured printer
  -> Queue the label job(s) through DeviceHub
  -> Continue with the next item in the Shipment
```

The customization does not accept or resolve Sales Order numbers. Shipment loading, allocation, location, item, lot/serial, UOM, quantity, short-pick behavior, picking completion, packing, and shipment confirmation remain native Acumatica behavior.

## Package Contents

| Artifact | Purpose |
| --- | --- |
| `src/ServaData.cs` | Label audit DAC, scanner context, and printer/history filters. |
| `src/ServaSOPickScanExtension.cs` | Native post-pick label count, per-label quantity, printer selection, and queue trigger. |
| `src/ServaLabelPrinting.cs` | DeviceHub submission and history/retry/reprint graph. |
| `database/SERVA_SO_PICK_FACADE_DB_20260911_01.sql` | Existing additive audit-table migration artifact. |
| `reports/SV619211.rpx` | Four-by-three-inch item pick label. |
| `pages/SV/SV619210.aspx` and `.aspx.cs` | Label Print History page and required `PXPage` code-behind. |
| `mobile/serva-so-pick-facade.msd` | Adds the default-printer print action to native mobile picking. |
| `tests/compile_against_acumatica.ps1` | Compile check for Acumatica 2026 R1. |
| `tests/validate_label_package.py` | Package contract validator. |

The historical SQL and mobile artifact filenames can remain because renaming customization artifacts after deployment creates unnecessary metadata conflicts. They no longer provide SO-first behavior.

## 1. Remove The Previous Runtime Code

1. Export the existing customization as a rollback package.
2. Open the current customization project or create `SERVA_SHIPMENT_PICK_LABELS` for a clean deployment.
3. Remove `ServaSOPickResolver.cs` from the project.
4. Replace the complete contents of `ServaSOPickScanExtension.cs`, `ServaData.cs`, and `ServaLabelPrinting.cs` with the packaged versions.
5. Confirm no published customization still contains `ServaSOPickResolver`, `DocumentTypeState`, `UsrServaDocumentType`, an SO/shipment mode prompt, or an override of `PickMode.ShipmentState`.
6. Publish with cleanup and restart the application when prompted.

Do not remove `dbo.ServaLabelPrintRequest`; it is the label audit table. No database change is required solely for this shipment-first revision because the new scanner fields are unbound session fields.

## 2. Database Migration For A New Installation

Skip this section when the existing migration has already published successfully.

1. Open **Database Scripts** in the Customization Project Editor.
2. Add `database/SERVA_SO_PICK_FACADE_DB_20260911_01.sql`.
3. Use Script Name `SERVA_SO_PICK_FACADE_DB_20260911_01`.
4. If that metadata name was previously used, increment only the suffix, for example `SERVA_SO_PICK_FACADE_DB_20260911_02`.
5. Publish the database script before publishing C# or the report.
6. Confirm `dbo.ServaLabelPrintRequest` exists with its company-aware primary key.

## 3. Add And Compile C#

1. Add these three C# files under namespace `Serva.Acumatica.SoScannerLabels`:
   - `ServaData.cs`
   - `ServaSOPickScanExtension.cs`
   - `ServaLabelPrinting.cs`
2. Publish the project.
3. Open `SO302020`, select Pick mode, and confirm the initial prompt is Acumatica's native Shipment prompt.
4. Confirm action `ServaSelectPrinter` is present on `SO302020` after publication. No custom Web Service screen or smart-panel mapping is required.

Local compile command:

```powershell
& ".\outputs\serva-so-scanner-label-printing\tests\compile_against_acumatica.ps1"
```

Expected: `Acumatica 2026 R1 compile passed against build 26.101.0225`.

## 4. Add Report SV619211

1. Publish the database and C# files first.
2. Open `reports/SV619211.rpx` in the Acumatica 2026 R1 Report Designer.
3. Connect to the tenant and verify the `ServaLabelPrintRequest` schema.
4. Confirm parameter `RequestID` is an integer and the report filter is `ServaLabelPrintRequest.RequestID = @RequestID`.
5. Confirm the custom page size is 4 by 3 inches and displayed quantity is `QuantityPerLabel`.
6. Save to the server with report ID `SV619211` and title `Serva Item Pick Tag`.
7. Add the report to the customization project.
8. Preview it using a real Request ID before testing DeviceHub.

## 5. Add History Page SV619210

The history page is optional for immediate picking and printing, but it is required for operational print history, Retry, and Reprint. On hosted Acumatica, create it as a **Screen** in the existing customization project. Do not use **Files > Add** because that dialog only lists files already present on the hosted website.

The business logic already exists in graph `Serva.Acumatica.SoScannerLabels.ServaLabelHistoryEntry`. The new screen uses a small wrapper graph because **Create New Screen** requires a unique graph name.

### 5.1 Back Up And Check Existing Items

1. Export the currently published customization as a rollback package.
2. Open the same project in the Customization Project Editor; it does not need to be unpublished merely to add the screen.
3. If a previous `SV619210` attempt exists under **Screens**, remove that screen item before recreating it.
4. Under **Code**, remove any earlier generated `ServaLabelHistoryPageShell` or duplicate `ServaLabelHistoryScreen` file.
5. Under **Files**, remove any manually added `Pages\SV\SV619210.aspx` or `SV619210.aspx.cs` item. The page must not exist under both **Screens** and **Files**.
6. Publish with cleanup. Confirm the existing three packaged C# files compile before adding the screen.

### 5.2 Create The Hosted Screen

1. Open **Screens** and select **Create New Screen**.
2. Enter exactly:
   - Screen ID: `SV619210`
   - Graph Name: `ServaLabelHistoryScreen`
   - Graph Namespace: `Serva.Acumatica.SoScannerLabels`
   - Page Title: `Label Print History`
   - Template: `FormGrid (FormDetail)`
3. Create and save the screen.
4. The wizard creates two different C# files: a business graph source and `SV619210.aspx.cs` page code-behind. Keep both at this stage.
5. Open only the generated business graph source and replace its entire contents, not only its namespace, with:

```csharp
namespace Serva.Acumatica.SoScannerLabels
{
    public class ServaLabelHistoryScreen : ServaLabelHistoryEntry
    {
    }
}
```

6. Remove any generated `PXGraph<ServaLabelHistoryScreen>` inheritance, views, DACs, constructors, and actions. The wrapper above must be the complete business graph file.
7. Do not replace or delete `SV619210.aspx.cs`. Confirm it contains `public partial class Page_SV619210 : PXPage`.
8. Save and publish. This intermediate publish lets Acumatica discover the inherited `Filter`, `Requests`, `Reprint`, and `Retry` members before the layout is configured.

### 5.3 Bind The Data Source

1. Reopen **Screens > SV619210** and select **Edit**.
2. Edit the generated ASPX through the **Screens** editor, not through **Files**.
3. Select the `PXDataSource` and set:
   - Type Name: `Serva.Acumatica.SoScannerLabels.ServaLabelHistoryScreen`
   - Primary View: `Filter`
4. Add callback commands `Reprint` and `Retry`, both with **Commit Changes** enabled.
5. Confirm the resulting data-source markup contains:

```aspx
TypeName="Serva.Acumatica.SoScannerLabels.ServaLabelHistoryScreen"
PrimaryView="Filter"
```

6. Confirm the first ASPX directive still contains `CodeFile="SV619210.aspx.cs"` and `Inherits="Page_SV619210"`.

### 5.4 Configure The Filter Form

1. Select the generated `PXFormView`.
2. Set Data Source ID to `ds` and Data Member to `Filter`.
3. Use **Add Data Fields** to add:
   - `ShipmentNbr`
   - `Status`
   - `FromDate`
   - `ToDate`
   - `LabelCount`
   - `Reason`
   - `PrinterID`
4. `ShipmentNbr` should render as a selector backed by Shipment headers, so each Shipment number appears once. `Status` should render as a drop-down containing `PENDING`, `SUBMITTED`, `FAILED`, and `SKIPPED`.
5. `PrinterID` should render as a selector because the field uses `PXPrinterSelector`.

### 5.5 Configure The History Grid

1. Select the generated `PXGrid`.
2. Set Data Source ID to `ds` and Sync Position to `True`. Leave the outer grid's Data Member blank; `PXGrid.DataMember` cannot be set declaratively in this page.
3. Select the grid level and set its Data Member to `Requests`. This is the only place the grid's `DataMember="Requests"` should appear.
4. Add these columns:
   - `RequestID`, `Source`, `ShipmentNbr`
   - `InventoryID`, `LocationID`
   - `PickedQty`, `QuantityPerLabel`, `UOM`, `LabelCount`
   - `PrinterName`, `Status`, `CreatedDateTime`
   - `ErrorMessage`
5. Use grid skin `PrimaryInquire` when available.
6. Do not add an `AutoSize` element. Confirm the ASPX contains no `Container=""` or `DataMember` attribute on the outer `<px:PXGrid>` element. The page directive must retain its valid `CodeFile` and `Inherits` values.

### 5.6 Publish And Diagnose

1. Save the screen and publish the existing project.
2. Open `SV619210` and confirm the filter form and print-history grid appear.
3. Add or confirm a Site Map node with Screen ID `SV619210`, title `Label Print History`, and URL `~/Pages/SV/SV619210.aspx`.
4. Grant supervisor/shipping roles access.
5. Do not add the generated ASPX separately under **Files**.

If publication remains at **Compilation** for more than several minutes, remove the screen and wrapper temporarily and publish the original three C# files alone. If code-only publication succeeds, the generated screen or duplicate page item is the fault. Capture the final publish-log lines and the newest `SM205000` exception; the word **Compilation** by itself does not identify the blocking file.

If validation reports **DataMember cannot be declarative**, open the ASPX and remove `DataMember="Requests"` from `<px:PXGrid ...>`. Keep `DataMember="Requests"` only on `<px:PXGridLevel ...>`.

If the Screen Editor reports that the page must inherit from `PXPage`, restore `CodeFile="SV619210.aspx.cs"` and `Inherits="Page_SV619210"` on the ASPX page directive, then confirm `SV619210.aspx.cs` defines `Page_SV619210 : PXPage`. Publish those page files before reopening the editor.

## 6. Configure Mobile Metadata

1. Open the customization project's **Mobile Application** editor.
2. Merge `mobile/serva-so-pick-facade.msd`.
3. Validate and publish the mobile sitemap.
4. Confirm `SO302020` exposes **Print Label** in its actions. The MSDL deliberately does not target `FooterLayout/Line1`, because `Line1` is not present in every 2026 R1 mobile definition.
5. Sign out of the mobile app, sign back in, and refresh mobile metadata.

If validation reports `can't find entity with key Line1`, remove the entire `update layout "FooterLayout"` block from the installed MSDL and merge the current packaged file again. The `ServaSelectPrinter` record action remains registered on `HeaderView` and does not require a `recordActionLink` tied to that layout.

The current MSDL intentionally contains no `update sitemap` block and does not add `SV619210` to the mobile menu. `SV619210` remains a web history/retry screen; no separate mobile sitemap screen needs to be found or configured.

## 7. Configure Native Picking

In **Sales Orders Preferences** (`SO101000`):

1. Enable Pick and Scan Log for the pilot.
2. Disable **Use Default Location** when pickers must scan the physical source location.
3. Enable **Use Explicit Line Confirmation**.
4. Disable **Use Default Quantity**. The extension also requires native quantity entry for positive picks.
5. Disable shipment confirmation on picking completion.
6. Select Path Priority or Pick Priority ordering.

Configure a unique barcode and the required path/pick priority for each pickable warehouse location. Create and release Shipments through the normal Acumatica order-processing workflow before warehouse picking starts.

## 8. Configure Printers And DeviceHub

1. Install DeviceHub from the same Acumatica build as the tenant.
2. Connect it to the correct Acumatica instance and tenant.
3. Install the Zebra Windows driver and configure 4-by-3-inch stock without scaling.
4. In **Printers** (`SM206510`), run **Update Printer List**.
5. Confirm each intended printer appears and can print a DeviceHub test page.
6. In **User Profile** (`SM203010`), set each picker's default printer. The **Print Label** action uses this printer unless the picker has scanned another printer for the current scanner session.
7. Grant pickers access to the configured printers and report `SV619211`.

For a positive label count, the scanner asks for the positive decimal quantity to print on each label and then waits at **Print Label**. Tap the action to queue with the session override or the user's default printer. To override it, scan the exact active Printer Name, DeviceHub ID, or printer GUID shown in `SM206510`; the override lasts for the scanner session. A queue/report failure records `Failed` but does not reverse the completed pick, and the scanner returns to location entry.

## 9. End-To-End Test

1. Create an open Shipment with at least two stock lines and valid allocations.
2. Enter Pick mode in `SO302020` and scan the Shipment number directly.
3. Confirm no Sales Order/Shipment choice appears.
4. Scan the first line's location, item, and lot/serial when applicable.
5. Enter the pick quantity and confirm it commits before the label prompt.
6. Enter label count `0`; confirm a Skipped row is recorded and the scanner asks for the next location without printer selection.
7. Pick the second line, enter label count `2`, and enter label quantity `5`.
8. Confirm the scanner waits at the printer stage and history shows Label Count `2` and Label Qty. `5`.
9. Tap **Print Label** and confirm it uses the picker's default printer. Confirm both labels show quantity `5`. Repeat by scanning a different active Printer Name from `SM206510` to test the session override.
10. Confirm a print-history row becomes Submitted and a row appears in **Print Jobs** (`SM206500`).
11. Confirm the scanner clears the prior location and asks for the next location.
12. Repeat with label count `3`; confirm three one-copy jobs are queued and every label shows the entered label quantity.
13. Verify blank, decimal, negative, and values above `99` are rejected for label count. Verify zero, negative, and nonnumeric label quantities are rejected.
14. Remove picked quantity and confirm no label workflow starts.
15. Disable DeviceHub and repeat; confirm the pick remains committed, the request is Failed, and the scanner continues.
16. Correct DeviceHub, then use Retry in `SV619210` instead of repeating the pick.
17. Complete picking and confirm the Shipment is not automatically confirmed.

## Troubleshooting Printing

### Shipment-Line Capture

The earlier context-capture errors came from waiting until the label-count callback to create the request and from relying on a current-row BQL binding. The revised scanner resolves `SOShipLine` by explicit Shipment and line keys and saves the Pending or Failed audit row immediately after native confirmation succeeds. Label count, label quantity, and printer selection update that persisted row, so a failed context capture is visible in `SV619210` and can be diagnosed without repeating the pick.

`SV619210` is only the web user interface for viewing requests and running Retry/Reprint; it does not participate in immediate label creation.

After publishing the latest scanner code, a positive label count should place the scanner at **Enter the quantity to print on each label**, followed by **Print Label**. The action submits with the effective default printer and does not open a smart panel. An `Incorrect mapping` exception means the old MSDL dialog or old C# `AskExt()` implementation is still published; remove the old mobile update, merge the packaged MSDL, republish, and refresh mobile metadata.

For the next reproduction, record the complete text after **label printing failed:** and check the following:

- Preview `SV619211` with the failed Request ID. A preview error is a report/schema issue, not DeviceHub.
- Read **Error** in `SV619210` and inspect **System Management > Trace** (`SM205000`).
- Check **Print Jobs** (`SM206500`). No row means report/job creation was rejected; an existing row moves troubleshooting to DeviceHub, the Windows queue, driver, or printer.
- Re-run **Update Printer List** in `SM206510` if the printer selector is empty.
- Confirm the signed-in user can access the printer and `SV619211`.

`Submitted` means Acumatica accepted the DeviceHub print job. It does not prove that paper physically exited the printer.

## Rollback

1. Unpublish the revised project.
2. Republish the exported rollback package only after confirming it does not contain duplicate graph classes.
3. Leave `ServaLabelPrintRequest` in place to preserve audit history.
4. Refresh mobile metadata after publishing or rolling back.
