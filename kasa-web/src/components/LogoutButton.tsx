import { useState } from 'react'
import { logout } from '../api'

/** Üst bardaki çıkış düğmesi; logout sunucuda oturumu geçersiz kılar, sonra login'e döner. */
export default function LogoutButton() {
  const [busy, setBusy] = useState(false)

  async function onClick() {
    setBusy(true)
    try {
      await logout()
    } catch {
      // Oturum zaten düşmüş olabilir; yine de login'e dön.
    }
    window.location.assign('/login')
  }

  return (
    <button type="button" className="btn-secondary btn-small btn-logout" onClick={onClick} disabled={busy}>
      Çıkış
    </button>
  )
}
