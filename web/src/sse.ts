/**
 * Incremental parser for a `text/event-stream` body. Feed it decoded text chunks as they arrive;
 * it calls `onData` once per complete event with the joined `data:` lines.
 */
export function createSseParser(onData: (data: string) => void) {
  let buffer = ''

  return (chunk: string) => {
    buffer += chunk.replace(/\r\n/g, '\n')

    let end: number
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const rawEvent = buffer.slice(0, end)
      buffer = buffer.slice(end + 2)

      const data = rawEvent
        .split('\n')
        .filter((line) => line.startsWith('data:'))
        .map((line) => line.slice(5).replace(/^ /, ''))
        .join('\n')
      if (data) onData(data)
    }
  }
}
