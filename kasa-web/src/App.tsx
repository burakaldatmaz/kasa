import AyPage from './AyPage'
import DayPage from './DayPage'
import LoginPage from './LoginPage'
import RaporPage from './RaporPage'

// Router bağımlılığı yok: dört sayfalık uygulamada path'e bakmak yeterli.
export default function App() {
  switch (window.location.pathname) {
    case '/login':
      return <LoginPage />
    case '/rapor':
      return <RaporPage />
    case '/ay':
      return <AyPage />
    default:
      return <DayPage />
  }
}
