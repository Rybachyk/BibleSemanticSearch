
import { useEffect, useState } from 'react'
import './App.css'
import {
  getSearchFilters,
  searchBible,
  getChapter,
  type BibleBook,
  type SearchResult,
  type TestamentOption,
  type ChapterResponse,
} from './services/searchService'

function App() {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SearchResult[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [searched, setSearched] = useState(false)

  const [testaments, setTestaments] = useState<TestamentOption[]>([])
  const [books, setBooks] = useState<BibleBook[]>([])
  const [selectedTestament, setSelectedTestament] = useState('')
  const [selectedBook, setSelectedBook] = useState('')
  const [filtersLoading, setFiltersLoading] = useState(true)

  // Стан відкритого біблійного розділу
  const [selectedVerseId, setSelectedVerseId] = useState<string | null>(null)
  const [chapterData, setChapterData] = useState<ChapterResponse | null>(null)
  const [chapterLoading, setChapterLoading] = useState(false)
  const [chapterError, setChapterError] = useState('')

  useEffect(() => {
    const loadFilters = async () => {
      try {
        const data = await getSearchFilters()
        setTestaments(data.testaments)
        setBooks(data.books)
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : 'Не вдалося завантажити фільтри.'
        )
      } finally {
        setFiltersLoading(false)
      }
    }

    loadFilters()
  }, [])

  const filteredBooks = books.filter(
    (book) =>
      !selectedTestament ||
      book.testament === selectedTestament
  )

  const handleTestamentChange = (value: string) => {
    setSelectedTestament(value)
    setSelectedBook('')
  }

  const handleOpenChapter = async (result: SearchResult) => {
    // Якщо натиснули на вже відкритий результат — закриваємо розділ
    if (selectedVerseId === result.id) {
      setSelectedVerseId(null)
      setChapterData(null)
      setChapterError('')
      return
    }

    setSelectedVerseId(result.id)
    setChapterData(null)
    setChapterError('')
    setChapterLoading(true)

    try {
      if (!result.documentId) {
        throw new Error(
          'У результаті пошуку відсутній documentId. Перевір SearchController.'
        )
      }

      const data = await getChapter(
        result.documentId,
        result.chapter
      )

      setChapterData(data)
    } catch (err) {
      setChapterError(
        err instanceof Error
          ? err.message
          : 'Не вдалося завантажити розділ.'
      )
    } finally {
      setChapterLoading(false)
    }
  }

  const handleSearch = async () => {
    if (!query.trim() || loading) return

    setLoading(true)
    setError('')
    setResults([])
    setSearched(false)

    // Закриваємо попередній розділ
    setSelectedVerseId(null)
    setChapterData(null)
    setChapterError('')

    try {
      const response = await searchBible({
        query: query.trim(),
        topK: 5,
        chunkTopK: 5,
        testament: selectedTestament || undefined,
        book: selectedBook || undefined,
      })

      setResults(response.results)
      setSearched(true)
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Не вдалося виконати пошук.'
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="app">
      <header className="header">
        <div className="logo">
          <div className="logo-icon">✝</div>
          <span>Bible Semantic Search</span>
        </div>
        <span className="translation">Переклад Івана Огієнка</span>
      </header>

      <main className="main">
        <div className="hero">
          <span className="eyebrow">СЕМАНТИЧНИЙ ПОШУК БІБЛІЇ</span>

          <h1>
            Знайди біблійні відповіді
            <br />
            на свої запитання
          </h1>

          <p className="description">
            Шукай не лише за словами, а й за змістом.
            <br />
            Опиши свою думку — знайди відповідні біблійні вірші.
          </p>

          <div className="search-box">
            <span className="search-icon">⌕</span>
            <input
              type="text"
              placeholder="Наприклад: Божа любов до людей..."
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') handleSearch()
              }}
            />
            <button
              onClick={handleSearch}
              disabled={loading || !query.trim()}
            >
              {loading ? 'Шукаю...' : 'Знайти'}
            </button>
          </div>

          <div className="filter-section">
            <div className="filter-group">
              <label htmlFor="testament-filter">Біблійний Завіт</label>
              <select
                id="testament-filter"
                value={selectedTestament}
                onChange={(e) => handleTestamentChange(e.target.value)}
                disabled={filtersLoading}
              >
                <option value="">Уся Біблія</option>
                {testaments.map((testament) => (
                  <option key={testament.value} value={testament.value}>
                    {testament.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="filter-group">
              <label htmlFor="book-filter">Біблійна книга</label>
              <select
                id="book-filter"
                value={selectedBook}
                onChange={(e) => setSelectedBook(e.target.value)}
                disabled={filtersLoading}
              >
                <option value="">Усі книги</option>
                {filteredBooks.map((book) => (
                  <option key={book.documentId} value={book.title}>
                    {book.title}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="examples">
            <span>Спробуй пошук:</span>
            {[
              'Божа любов до людей',
              'Віра під час випробувань',
              'Прощення ворогів',
            ].map((example) => (
              <button
                key={example}
                className="example"
                onClick={() => setQuery(example)}
              >
                {example}
              </button>
            ))}
          </div>
        </div>

        {loading && (
          <div className="search-status">
            Виконується семантичний пошук...
          </div>
        )}

        {error && (
          <div className="error-message">
            {error}
          </div>
        )}

        {searched && (
          <section className="results-section">
            <h2>Результати пошуку</h2>
            <p className="results-count">
              Знайдено віршів: {results.length}
            </p>

            {results.length === 0 ? (
              <p>За цим запитом нічого не знайдено.</p>
            ) : (
              <div className="results-list">
                {results.map((result) => {
                  const isSelected = selectedVerseId === result.id

                  return (
                    <div key={result.id}>
                      <article
                        className={`result-card ${isSelected ? 'selected' : ''}`}
                        onClick={() => handleOpenChapter(result)}
                        role="button"
                        tabIndex={0}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault()
                            handleOpenChapter(result)
                          }
                        }}
                        aria-expanded={isSelected}
                      >
                        <div className="result-header">
                          <h3>
                            {result.book} {result.chapter}:{result.number}
                          </h3>
                          <span className="score">
                            Score: {result.score.toFixed(4)}
                          </span>
                        </div>

                        <p className="verse-text">{result.text}</p>

                        <span className="result-translation">
                          Переклад Івана Огієнка
                        </span>

                        <div className="chapter-hint">
                          {isSelected
                            ? 'Закрити розділ ▲'
                            : 'Переглянути весь розділ ▼'}
                        </div>
                      </article>

                      {isSelected && (
                        <div className="chapter-panel">
                          {chapterLoading && (
                            <p className="search-status">
                              Завантаження розділу...
                            </p>
                          )}

                          {chapterError && (
                            <div className="error-message">
                              {chapterError}
                            </div>
                          )}

                          {chapterData && (
                            <>
                              <h3 className="chapter-title">
                                {chapterData.book} — розділ {chapterData.chapter}
                              </h3>

                              <div className="chapter-verses">
                                {chapterData.verses.map((verse) => (
                                  <p
                                    key={verse.id}
                                    className={`chapter-verse ${
                                      verse.number === result.number
                                        ? 'highlighted-verse'
                                        : ''
                                    }`}
                                  >
                                    <span className="chapter-verse-number">
                                      {verse.number}
                                    </span>
                                    {verse.text}
                                  </p>
                                ))}
                              </div>

                              <span className="result-translation">
                                Переклад Івана Огієнка
                              </span>
                            </>
                          )}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            )}
          </section>
        )}

        <section className="info-section">
          <div className="info-card">
            <span className="info-number">01</span>
            <h3>Опиши думку</h3>
            <p>
              Введи запит звичайною мовою, не шукаючи точних слів із Біблії.
            </p>
          </div>

          <div className="info-card">
            <span className="info-number">02</span>
            <h3>Семантичний аналіз</h3>
            <p>
              Система аналізує зміст запиту та знаходить близькі за значенням тексти.
            </p>
          </div>

          <div className="info-card">
            <span className="info-number">03</span>
            <h3>Отримай вірші</h3>
            <p>
              Переглядай найбільш релевантні біблійні уривки.
            </p>
          </div>
        </section>
      </main>

      <footer className="footer">
        <span>Semantic Bible Search</span>
        <span>Біблія в перекладі Івана Огієнка</span>
      </footer>
    </div>
  )
}

export default App
