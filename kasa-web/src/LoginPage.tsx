import { useState } from 'react'
import type { FormEvent } from 'react'
import { errorMessage, login } from './api'

/** next yalnız site içi path olabilir (open redirect koruması). */
function nextFromUrl(): string {
  const param = new URLSearchParams(window.location.search).get('next')
  return param !== null && param.startsWith('/') && !param.startsWith('//') ? param : '/'
}

/** Tek kullanıcılı giriş: doğru parola → cookie oturumu, geldiği sayfaya dönüş. */
export default function LoginPage() {
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      await login(password)
      window.location.assign(nextFromUrl())
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <div className="login-page">
      <form className="card login-card" onSubmit={onSubmit}>
        <h1 className="card-header">Günlük Kasa — Giriş</h1>
        <div className="card-body">
          <label className="field">
            <span>Parola</span>
            <input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              autoFocus
              autoComplete="current-password"
            />
          </label>
          {error && <p className="form-error">{error}</p>}
          <button type="submit" className="btn-primary login-submit" disabled={busy || password.length === 0}>
            {busy ? 'Giriş yapılıyor…' : 'Giriş Yap'}
          </button>
        </div>
      </form>
    </div>
  )
}
