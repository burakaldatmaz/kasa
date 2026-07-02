import AyPage from './AyPage'
import DayPage from './DayPage'
import RaporPage from './RaporPage'

// Router bağımlılığı yok: üç sayfalık uygulamada path'e bakmak yeterli.
export default function App() {
  switch (window.location.pathname) {
    case '/rapor':
      return <RaporPage />
    case '/ay':
      return <AyPage />
    default:
      return <DayPage />
  }
}
