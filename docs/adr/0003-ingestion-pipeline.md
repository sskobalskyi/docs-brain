# 3. Synchronous ingestion with pluggable text extractors

## Status
Accepted

## Context
Uploaded files must become searchable chunks. Supported formats: PDF, DOCX, TXT and Markdown, up to 10 MB.

## Decision
- `ITextExtractor` per format (PdfPig, Open XML SDK, plain text). Adding a format means adding one class and one registration.
- `TextChunker` is a pure function in `DocsBrain.Core`: paragraph-aligned chunks of up to 1000 characters with a 150 character overlap. Overlap is cut at a word boundary.
- Ingestion runs inside the upload request and returns typed `Result` errors (`ingestion.unsupported_type`, `ingestion.unreadable`, `ingestion.empty`), mapped to ProblemDetails with a `code` extension.
- Chunks are tenant-owned and covered by the same query filter as documents.

## Consequences
- Simple and testable. Large files block the request; embedding (next step) will move ingestion to a background queue if upload latency becomes a problem.
- Scanned PDFs without a text layer are rejected as empty. OCR is out of scope.
