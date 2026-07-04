// BKKBIKE aktif filo — Vehicle export (Status=1), tek kaynak.
// Liste sık değişmez; değişince bu dosyayı güncelle + build al. DB/migration yok.
//
// deposit (THB): ADV 350 → 5000, diğerleri → 3000.
// Km limitleri (aşım her grupta 2 ฿/km):
//   bangkok-only  (Scoopy, Click 125): 100 km/gün, Bangkok dışına çıkış YOK
//   within-150    (Click 160, PCX, Nmax): 150 km/gün, Bangkok'tan max 150 km
//   unlimited     (ADV 160, ADV 350): 250 km/gün, mesafe limiti yok

export type RadiusPolicy = 'bangkok-only' | 'within-150' | 'unlimited'

export interface Vehicle {
  plate: string
  model: string
  dailyFee: number        // THB/gün, bilgi amaçlı
  deposit: number         // THB, forma otomatik önerilir (override edilebilir)
  dailyKm: number         // günlük km limiti
  radiusPolicy: RadiusPolicy
}

export const EXCESS_KM_FEE_THB = 2  // limit aşımı: ฿/km (tüm araçlar)

export const VEHICLES: Vehicle[] = [
  { plate: "8 ขพ 7303", model: "Honda ADV 160 2025 Black", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "8 ขผ 7250", model: "Honda ADV 160 2025 Red", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆจ 4129", model: "Honda ADV 160 Black", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆจ 4132", model: "Honda ADV 160 Black", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆถ 370", model: "Honda ADV 160 Black", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "9 ขข 4344", model: "Honda ADV 160 Green", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆม 3614", model: "Honda ADV 160 Matt Sahara", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆจ 4169", model: "Honda ADV 350 RoadSync Black", dailyFee: 1350, deposit: 5000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆจ 4190", model: "Honda ADV 350 RoadSync Black", dailyFee: 1350, deposit: 5000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆฐ 5250", model: "Honda ADV 350 RoadSync Black", dailyFee: 1350, deposit: 5000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "8 ขผ 3163", model: "Honda ADV 350 RoadSync Black", dailyFee: 1350, deposit: 5000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "8 ขว 8096", model: "Honda ADV 350 RoadSync Gray", dailyFee: 1350, deposit: 5000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "8 ขศ 9918", model: "Honda ADV 350 RoadSync Red", dailyFee: 1350, deposit: 5000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "1 ฆจ 4137", model: "Honda ADV160 Gray", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "8 ขน 1755", model: "Honda Click 125 Black", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขฮ 4757", model: "Honda Click 125 Black", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "9 ขก 2315", model: "Honda Click 125 Black", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "9 ขก 2317", model: "Honda Click 125 Black", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "9 ขก 2322", model: "Honda Click 125 Black", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "9 ขข 6991", model: "Honda Click 125 Black", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขร 8232", model: "Honda Click 125 Gray", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขร 8235", model: "Honda Click 125 Gray", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "9 ขข 4339", model: "Honda Click 125 Gray", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "9 ขข 6982", model: "Honda Click 125 Gray", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขธ 1743", model: "Honda Click 125 Grey", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขน 1762", model: "Honda Click 125 Grey", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขร 3422", model: "Honda Click 125 Grey", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขน 1758", model: "Honda Click 125 Red", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขพ 2352", model: "Honda Click 125 Red", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขพ 2357", model: "Honda Click 125 Red", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขพ 7288", model: "Honda Click 125 Red", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขพ 7296", model: "Honda Click 125 Red", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "9 ขข 4348", model: "Honda Click 125 Red", dailyFee: 350, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขฮ 4758", model: "Honda Click 160 Black", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ฃพ 2361", model: "Honda Click 160 Black", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขฮ 4761", model: "Honda Click 160 Gray", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขพ 7283", model: "Honda Click 160 Grey", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขล 2033", model: "Honda Click 160 Grey", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขล 2034", model: "Honda Click 160 Grey", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขน 1754", model: "Honda Click 160 Red", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขผ 7252", model: "Honda Click 160 Red", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขฮ 4753", model: "Honda Click 160 Red", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "8 ขธ 1742", model: "Honda Click 160 SE Black", dailyFee: 450, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "1 ฆฒ 1072", model: "Honda New ADV 160", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "9 ขง 3997", model: "Honda New ADV 160 Red", dailyFee: 600, deposit: 3000, dailyKm: 250, radiusPolicy: "unlimited" },
  { plate: "9 ขข 6994", model: "Honda New PCX 160 Gray", dailyFee: 650, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "9 ขข 4334", model: "Honda New PCX 160 Red", dailyFee: 650, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "1 ฆจ 4191", model: "Honda PCX 160 Gray", dailyFee: 600, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "1 ฆจ 4167", model: "Honda PCX 160 Matt Black", dailyFee: 650, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "1 ฆจ 4200", model: "Honda PCX 160 Matt Black", dailyFee: 650, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
  { plate: "1 ฆจ 4197", model: "Honda Scoopy Black", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "1 ฆจ 4194", model: "Honda Scoopy Blue", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขผ 3167", model: "Honda Scoopy Club12 Black", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขร 3405", model: "Honda Scoopy Club12 Blue-White", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขร 3410", model: "Honda Scoopy Club12 Blue-White", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "1 ฆจ 4141", model: "Honda Scoopy Red", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขฮ 2669", model: "Honda Scoopy Red", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "8 ขฮ 2672", model: "Honda Scoopy Red", dailyFee: 300, deposit: 3000, dailyKm: 100, radiusPolicy: "bangkok-only" },
  { plate: "1 ฆภ 1326", model: "Yamaha Nmax 155", dailyFee: 600, deposit: 3000, dailyKm: 150, radiusPolicy: "within-150" },
]

export const VEHICLE_BY_PLATE: Record<string, Vehicle> =
  Object.fromEntries(VEHICLES.map(v => [v.plate, v]))
