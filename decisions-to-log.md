# Decisions To Record

| Area | Decision | Rationale |
| --- | --- | --- |
| Version | This customization targets Acumatica 2026 R1. | The customization was compiled against build 26.101.0225; this supersedes the older 2025 R2 project-brief reference for this package. |
| Entry point | Pickers scan only a Shipment or native pick-list barcode in `SO302020`. | Keeps the warehouse execution document explicit and leaves Sales Orders in order processing. |
| Document identity | The customization does not interpret Sales Order numbers at the shipment prompt. | Removes collisions and avoids blurring order demand with released warehouse work. |
| Shipment policy | Shipments are created and released before picking through native order processing. | Keeps shipment composition and release away from the warehouse scanner. |
| Picking records | Native shipment lines, splits, allocations, and picked quantities are authoritative. | Avoids a parallel pick ledger and preserves downstream packing/confirmation. |
| Pick quantity | Every positive Pick-mode operation uses Acumatica's native quantity state before confirmation. | Prevents the native default quantity from being committed before the operative supplies the intended amount. |
| Staging/container | No SHIP01, staging confirmation, or Container ID is captured. | These values are not required for the agreed workflow. |
| Labels | Prompt for label count `0-99` after each successful positive pick; each label shows the quantity just picked. | Separates number of physical labels from inventory quantity. |
| Printer | After a positive label count, select a configured printer from `PXPrinterSelector`; preselect the user's default and permit a scanned printer GUID as fallback. | Makes the destination explicit for each print request while retaining a scanner-only recovery path. |
| Printing | Applying printer selection submits report `SV619211` through DeviceHub as one copy per job, then returns to native picking. | Keeps print failure separate from the committed pick and gives deterministic copy handling. |
| Reprints | Reprint/retry starts from immutable print history and retains SO/shipment/line context. | Preserves traceability without changing picked quantity. |
| Completion | Picking completion does not confirm the shipment. | Packing and shipment confirmation remain separate native processes. |
