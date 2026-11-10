import { useEffect, useState } from 'react'
import { api } from './api'

/** harness.yaml in the browser. The server validates before writing; the API hot-reloads it. */
export function Config({ onSaved }: { onSaved: () => void }) {
  const [yaml, setYaml] = useState('')
  const [errors, setErrors] = useState<string[]>([])
  const [saved, setSaved] = useState(false)
  const [models, setModels] = useState<string[]>([])

  useEffect(() => {
    api.config().then(setYaml)
    api.models().then(m => setModels(m.map(x => x.id))).catch(() => {})
  }, [])

  async function save() {
    const errs = await api.saveConfig(yaml)
    setErrors(errs)
    setSaved(errs.length === 0)
    if (errs.length === 0) onSaved()
  }

  return (
    <div className="config">
      <div className="row">
        <button onClick={save}>Save</button>
        {saved && <span className="ok">Saved and reloaded.</span>}
      </div>
      {errors.length > 0 && <ul className="error">{errors.map(e => <li key={e}>{e}</li>)}</ul>}
      <textarea aria-label="harness.yaml" spellCheck={false} value={yaml} onChange={e => { setYaml(e.target.value); setSaved(false) }} />
      {models.length > 0 && <p className="muted">Copilot models available to you: {models.join(', ')}</p>}
    </div>
  )
}
