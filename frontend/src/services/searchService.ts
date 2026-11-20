
const API_URL = import.meta.env.VITE_API_URL

export interface SearchRequest {
  query: string
  topK: number
  chunkTopK: number
  documentId?: string
  testament?: string
  book?: string
}

export interface SearchResult {
  id: string
  documentId: string
  book: string
  chapter: number
  number: number
  text: string
  translation: string
  score: number
}

export interface SearchResponse {
  query: string
  chunksFound: number
  versesFound: number
  results: SearchResult[]
}

export interface ChapterVerse {
  id: string
  number: number
  text: string
}

export interface ChapterResponse {
  documentId: string
  book: string
  chapter: number
  translation: string
  verses: ChapterVerse[]
}

export interface TestamentOption {
  value: string
  label: string
}

export interface BibleBook {
  documentId: string
  title: string
  testament: string
  order: number
}

export interface SearchFiltersResponse {
  testaments: TestamentOption[]
  books: BibleBook[]
}

export async function getSearchFilters(): Promise<SearchFiltersResponse> {
  const response = await fetch(`${API_URL}/api/Search/filters`)

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(`Не вдалося завантажити фільтри: ${errorText}`)
  }

  return response.json() as Promise<SearchFiltersResponse>
}

export async function searchBible(
  request: SearchRequest
): Promise<SearchResponse> {
  const response = await fetch(`${API_URL}/api/Search`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(`Помилка пошуку (${response.status}): ${errorText}`)
  }

  return response.json() as Promise<SearchResponse>
}

export async function getChapter(
  documentId: string,
  chapter: number
): Promise<ChapterResponse> {
  const response = await fetch(
    `${API_URL}/api/Search/chapter/${encodeURIComponent(documentId)}/${chapter}`
  )

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(`Не вдалося завантажити розділ: ${errorText}`)
  }

  return response.json() as Promise<ChapterResponse>
}
