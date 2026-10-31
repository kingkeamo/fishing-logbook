# Native add / edit catch (offline)

The MAUI `CatchEditPage` records and edits catches through `ICatchStore` only. Saved catches appear in `CatchListViewModel`; draft catches autosave while editing and restore when opening **Record catch** again.

Photograph EXIF proposals use the shared `CatchPhotographProposalService` (same rules as the web editors). Saving never calls the network; synchronisation is a separate foreground coordinator over the outbox (see [mobile-catch-sync.md](mobile-catch-sync.md)). Trip association is out of scope for this flow.
