const foundationItems = [
  'React and TypeScript',
  'ASP.NET Core API',
  'Modular monolith boundaries',
]

export function App() {
  return (
    <main className="foundation-page">
      <section className="foundation-card" aria-labelledby="foundation-title">
        <p className="eyebrow">TP-002 · Foundation</p>
        <h1 id="foundation-title">TeamPulse</h1>
        <p className="summary">
          Student Project &amp; Engineering Collaboration Hub
        </p>

        <ul className="foundation-list">
          {foundationItems.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>

        <p className="status" role="status">
          Frontend foundation is ready.
        </p>
      </section>
    </main>
  )
}
