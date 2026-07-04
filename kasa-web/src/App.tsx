import AyPage from './AyPage'
import DayPage from './DayPage'
import DepozitoPage from './DepozitoPage'
import LoginPage from './LoginPage'
import RaporPage from './RaporPage'
import MobileApp from './mobile/MobileApp'
import { useMediaQuery } from './useMediaQuery'

// Router bağımlılığı yok: dört sayfalık uygulamada path'e bakmak yeterli.
// ≤640px: sekmeli mobil uygulama (MobileApp); 641px+ desktop düzeni aynen (R3).
export default function App() {
  const mobile = useMediaQuery('(max-width: 640px)')

  if (window.location.pathname === '/login') return <LoginPage />
  if (mobile) return <MobileApp />

  switch (window.location.pathname) {
    case '/rapor':
      return <RaporPage />
    case '/ay':
      return <AyPage />
    case '/depozito':
      return <DepozitoPage />
    default:
      return <DayPage />
  }
}
