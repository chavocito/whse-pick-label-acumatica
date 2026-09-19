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
| Labels | Prompt for label count `0-99` after each successful positive pick; for a positive count, require the quantity to print on each label. | Supports packaging one picked quantity across multiple boxes while preserving both picked quantity and label quantity. |
| Printer | The Print Label action uses the scanner-session override or the user's default printer; an override can be scanned by active Printer Name, DeviceHub ID, or GUID. | Avoids an unsupported mobile smart-panel mapping while retaining a scanner-only override path. |
| Printing | After label quantity entry, Print Label generates one raw `.zpl` file per label and submits it through DeviceHub, then returns to native picking. | Removes the RPX dependency while keeping print failure separate from the committed pick and copy handling deterministic. |
| Label country | The label prints the sales-order branch/company country, not the inventory item's country of origin. | Matches the approved `sv619210.zpl` field semantics. |
| Reprints | Reprint/retry starts from immutable print history and retains SO/shipment/line context and quantity per label. | Preserves the original label content without changing picked quantity. |
| Completion | Picking completion does not confirm the shipment. | Packing and shipment confirmation remain separate native processes. |
