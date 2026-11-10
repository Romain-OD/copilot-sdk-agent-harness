import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { HttpAgent, type Message } from '@ag-ui/client'

/** Streaming chat over AG-UI. The server picks the engine from the profile in the URL. */
export function Chat({ profile }: { profile: string }) {
  const agent = useMemo(() => new HttpAgent({ url: `/agui/${profile}` }), [profile])
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => {
    const sub = agent.subscribe({ onMessagesChanged: ({ messages }) => setMessages([...messages]) })
    return () => sub.unsubscribe()
  }, [agent])

  async function send(e: FormEvent) {
    e.preventDefault()
    if (!input.trim() || busy) return
    agent.addMessage({ id: crypto.randomUUID(), role: 'user', content: input })
    setInput('')
    setBusy(true)
    setError(undefined)
    try { await agent.runAgent() } catch (err) { setError(String(err)) } finally { setBusy(false) }
  }

  return (
    <div className="chat">
      <div className="messages">
        {messages.length === 0 && <p className="muted">Talking to <b>{profile}</b>. Tool calls go through the policy in harness.yaml; every run lands in the Runs tab.</p>}
        {messages.map(m => (
          <div key={m.id} className={`msg ${m.role}`}>
            <span className="role">{m.role}</span>
            <div>{typeof m.content === 'string' ? m.content : JSON.stringify(m.content)}</div>
          </div>
        ))}
        {busy && <p className="muted">thinking…</p>}
        {error && <p className="error">{error}</p>}
      </div>
      <form onSubmit={send}>
        <input aria-label="Prompt" value={input} onChange={e => setInput(e.target.value)} placeholder="Ask something…" autoFocus />
        <button disabled={busy}>Send</button>
        {busy && <button type="button" onClick={() => agent.abortRun()}>Stop</button>}
      </form>
    </div>
  )
}
