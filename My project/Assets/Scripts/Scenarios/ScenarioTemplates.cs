using System;

// Gameplay-scale, historically inspired starting points. They deliberately
// favor readable fronts and interesting terrain over literal cartography.
public static class ScenarioTemplates
{
    public static readonly EasternFrontTemplate[] All =
    {
        EasternFrontTemplate.OperationTyphoon1941,
        EasternFrontTemplate.Stalingrad1942,
        EasternFrontTemplate.Kursk1943,
        EasternFrontTemplate.OperationBagration1944,
        EasternFrontTemplate.Sandbox
    };

    public static ScenarioMap Create(EasternFrontTemplate template) => template switch
    {
        EasternFrontTemplate.Stalingrad1942 => CreateStalingrad(),
        EasternFrontTemplate.Kursk1943 => CreateKursk(),
        EasternFrontTemplate.OperationBagration1944 => CreateBagration(),
        EasternFrontTemplate.Sandbox => CreateSandbox(),
        _ => CreateTyphoon()
    };

    public static string ShortName(EasternFrontTemplate template) => template switch
    {
        EasternFrontTemplate.OperationTyphoon1941 => "OPERATION TYPHOON  ·  1941",
        EasternFrontTemplate.Stalingrad1942 => "STALINGRAD  ·  1942",
        EasternFrontTemplate.Kursk1943 => "KURSK  ·  1943",
        EasternFrontTemplate.OperationBagration1944 => "OPERATION BAGRATION  ·  1944",
        _ => "SANDBOX  ·  BLANK"
    };

    private static ScenarioMap CreateTyphoon()
    {
        var map = BaseMap("Operation Typhoon", "October 1941",
            "The German drive from Smolensk and Vyazma toward Moscow. Cities and operational towns use geographic positions; terrain remains gameplay-scaled.",
            1941, 36000, 40000, 31.5d, 39.5d, 52.4d, 57.2d);
        AddVerticalRiver(map, 103f, 7f, 0.11f, 19, 7);
        AddVerticalRiver(map, 130f, 4f, 0.08f, 23, 12);
        SetFront(map, y => 76f + 7f * (float)Math.Sin(y * 0.14f) +
            (Math.Abs(y - 49) < 15 ? 13f : 0f));
        AddTerrainBand(map, 72, 93, 8, 88, TerrainType.Forest, 43);
        AddTyphoonSettlements(map);
        AddTyphoonInfrastructure(map);
        return map;
    }

    private static ScenarioMap CreateStalingrad()
    {
        var map = BaseMap("Stalingrad", "August 1942",
            "The southern campaign from the Don basin to Stalingrad and the lower Volga. Regional cities and battle-area towns are geographically placed.",
            1942, 42000, 39000, 39d, 48.6d, 46d, 52d);
        AddVerticalRiver(map, 93f, 3f, 0.1f, 31, 16);
        AddVerticalRiver(map, 55f, 5f, 0.075f, 21, 4);
        SetFront(map, y => 79f + 5f * (float)Math.Sin(y * 0.1f) -
            (Math.Abs(y - 47) < 12 ? 10f : 0f));
        AddTerrainBand(map, 76, 102, 37, 58, TerrainType.Hills, 61);
        AddTerrainBand(map, 14, 91, 5, 29, TerrainType.Desert, 27);
        AddStalingradSettlements(map);
        AddStalingradInfrastructure(map);
        return map;
    }

    private static ScenarioMap CreateKursk()
    {
        var map = BaseMap("Kursk", "July 1943",
            "The Kursk salient between Oryol, Belgorod, and Kharkov. The city and town network is geographically projected across the battlefield.",
            1943, 46000, 43000, 34.3d, 40d, 49d, 53.5d);
        AddVerticalRiver(map, 132f, 5f, 0.09f, 20, 9);
        SetFront(map, y =>
        {
            float distance = (y - 57f) / 11f;
            return 76f - 38f * (float)Math.Exp(-distance * distance) + 2f * (float)Math.Sin(y * 0.2f);
        });
        AddTerrainBand(map, 42, 92, 24, 78, TerrainType.Hills, 35);
        AddKurskSettlements(map);
        AddKurskInfrastructure(map);
        return map;
    }

    private static ScenarioMap CreateBagration()
    {
        var map = BaseMap("Operation Bagration", "June 1944",
            "The Soviet offensive across Belarus, from the eastern fortified cities toward Minsk and the western rail centers. Settlements are geographically placed.",
            1944, 52000, 41000, 23d, 33d, 51.5d, 56d);
        AddVerticalRiver(map, 73f, 7f, 0.1f, 17, 6);
        AddVerticalRiver(map, 112f, 5f, 0.13f, 19, 2);
        SetFront(map, y => 105f + 8f * (float)Math.Sin(y * 0.12f) + 3f * (float)Math.Sin(y * 0.31f));
        AddTerrainBand(map, 35, 118, 12, 84, TerrainType.Forest, 28);
        AddMarshes(map);
        AddBagrationSettlements(map);
        AddBagrationInfrastructure(map);
        return map;
    }

    private static ScenarioMap CreateSandbox()
    {
        const int width = 160, height = 96;
        var map = new ScenarioMap("Sandbox", "Blank canvas",
            "A completely neutral plains map with no settlements or front line. Paint terrain and both starting sides to build a campaign from scratch.",
            width, height, 40000, 40000, 0d, 1d, 0d, 1d);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            map.SetTerrain(x, y, TerrainType.Land);
        return map;
    }

    private static ScenarioMap BaseMap(string name, string period, string description,
        int seed, int playerManpower, int enemyManpower, double westLongitude,
        double eastLongitude, double southLatitude, double northLatitude)
    {
        const int width = 160, height = 96;
        var map = new ScenarioMap(name, period, description, width, height, playerManpower, enemyManpower,
            westLongitude, eastLongitude, southLatitude, northLatitude);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            uint value = Hash(x, y, seed) % 100;
            TerrainType terrain = value < 15 ? TerrainType.Forest :
                value < 21 ? TerrainType.Hills : value < 23 ? TerrainType.Mountains : TerrainType.Land;
            map.SetTerrain(x, y, terrain);
        }
        return map;
    }

    // Operational coverage follows the place names shown on U.S. Army / West
    // Point campaign maps. Coordinates use the WGS84 placename records from
    // GeoNames; wartime names are retained where they improve historical fit.
    private static void AddTyphoonSettlements(ScenarioMap map)
    {
        map.AddSettlement("Moscow", 55.7520d, 37.6178d, true);
        map.AddSettlement("Smolensk", 54.7783d, 32.0509d, true);
        map.AddSettlement("Bryansk", 53.2521d, 34.3717d, true);
        map.AddSettlement("Oryol", 52.9651d, 36.0785d, true);
        map.AddSettlement("Tula", 54.1931d, 37.6173d, true);
        map.AddSettlement("Kalinin", 56.8584d, 35.9006d, true);
        map.AddSettlement("Kaluga", 54.5293d, 36.2754d, true);
        map.AddSettlement("Rzhev", 56.2624d, 34.3282d);
        map.AddSettlement("Vyazma", 55.2103d, 34.2951d);
        map.AddSettlement("Roslavl", 53.9528d, 32.8639d);
        map.AddSettlement("Gzhatsk", 55.5529d, 34.9954d);
        map.AddSettlement("Yukhnov", 54.7446d, 35.2297d);
        map.AddSettlement("Mozhaysk", 55.5067d, 36.0273d);
        map.AddSettlement("Volokolamsk", 56.0357d, 35.9585d);
        map.AddSettlement("Klin", 56.3333d, 36.7333d);
        map.AddSettlement("Dmitrov", 56.3442d, 37.5204d);
        map.AddSettlement("Istra", 55.9142d, 36.8603d);
        map.AddSettlement("Naro-Fominsk", 55.3862d, 36.7345d);
        map.AddSettlement("Podolsk", 55.4242d, 37.5547d);
        map.AddSettlement("Serpukhov", 54.9158d, 37.4111d);
        map.AddSettlement("Maloyaroslavets", 55.0146d, 36.4719d);
        map.AddSettlement("Kashira", 54.8444d, 38.1669d);
        map.AddSettlement("Kolomna", 55.0794d, 38.7783d);
    }

    private static void AddStalingradSettlements(ScenarioMap map)
    {
        map.AddSettlement("Stalingrad", 48.7080d, 44.5133d, true);
        map.AddSettlement("Rostov-on-Don", 47.2313d, 39.7233d, true);
        map.AddSettlement("Voronezh", 51.6720d, 39.1843d, true);
        map.AddSettlement("Saratov", 51.5336d, 46.0343d, true);
        map.AddSettlement("Astrakhan", 46.3497d, 48.0408d, true);
        map.AddSettlement("Borisoglebsk", 51.3682d, 42.0849d);
        map.AddSettlement("Boguchar", 49.9358d, 40.5456d);
        map.AddSettlement("Millerovo", 48.9227d, 40.3967d);
        map.AddSettlement("Novocherkassk", 47.4209d, 40.0917d);
        map.AddSettlement("Morozovsk", 48.3549d, 41.8263d);
        map.AddSettlement("Tatsinskaya", 48.1968d, 41.2756d);
        map.AddSettlement("Serafimovich", 49.5786d, 42.7360d);
        map.AddSettlement("Kletskaya", 49.3145d, 43.0584d);
        map.AddSettlement("Surovikino", 48.6073d, 42.8462d);
        map.AddSettlement("Kalach", 48.6900d, 43.5300d);
        map.AddSettlement("Nizhne-Chirskaya", 48.3597d, 43.0861d);
        map.AddSettlement("Kotelnikovo", 47.6317d, 43.1461d);
        map.AddSettlement("Frolovo", 49.7713d, 43.6622d);
        map.AddSettlement("Kamyshin", 50.0983d, 45.4160d);
        map.AddSettlement("Pallasovka", 50.0502d, 46.8836d);
        map.AddSettlement("Elista", 46.3077d, 44.2698d);
    }

    private static void AddKurskSettlements(ScenarioMap map)
    {
        map.AddSettlement("Kursk", 51.7269d, 36.1846d, true);
        map.AddSettlement("Oryol", 52.9651d, 36.0785d, true);
        map.AddSettlement("Belgorod", 50.5954d, 36.5873d, true);
        map.AddSettlement("Kharkov", 49.9935d, 36.2304d, true);
        map.AddSettlement("Sumy", 50.9077d, 34.7981d, true);
        map.AddSettlement("Voronezh", 51.6720d, 39.1843d, true);
        map.AddSettlement("Maloarkhangelsk", 52.4008d, 36.5033d);
        map.AddSettlement("Ponyri", 52.3184d, 36.3030d);
        map.AddSettlement("Fatezh", 52.0919d, 35.8591d);
        map.AddSettlement("Zolotukhino", 52.0842d, 36.3777d);
        map.AddSettlement("Dmitriyev-Lgovsky", 52.1280d, 35.0750d);
        map.AddSettlement("Rylsk", 51.5680d, 34.6820d);
        map.AddSettlement("Lgov", 51.6600d, 35.2600d);
        map.AddSettlement("Oboyan", 51.2110d, 36.2766d);
        map.AddSettlement("Prokhorovka", 51.0374d, 36.7327d);
        map.AddSettlement("Stary Oskol", 51.2967d, 37.8417d);
        map.AddSettlement("Korocha", 50.8132d, 37.1892d);
        map.AddSettlement("Yakovlevo", 50.8603d, 36.4472d);
        map.AddSettlement("Tomarovka", 50.6834d, 36.2331d);
        map.AddSettlement("Tim", 51.6222d, 37.1244d);
        map.AddSettlement("Sudzha", 51.1976d, 35.2726d);
        map.AddSettlement("Grayvoron", 50.4767d, 35.6750d);
    }

    private static void AddBagrationSettlements(ScenarioMap map)
    {
        map.AddSettlement("Minsk", 53.9002d, 27.5665d, true);
        map.AddSettlement("Vitebsk", 55.1904d, 30.2049d, true);
        map.AddSettlement("Mogilev", 53.9088d, 30.3404d, true);
        map.AddSettlement("Bobruisk", 53.1468d, 29.2055d, true);
        map.AddSettlement("Orsha", 54.5136d, 30.4036d, true);
        map.AddSettlement("Gomel", 52.4345d, 30.9754d, true);
        map.AddSettlement("Baranovichi", 53.1326d, 26.0078d, true);
        map.AddSettlement("Vilnius", 54.6872d, 25.2797d, true);
        map.AddSettlement("Grodno", 53.6758d, 23.8289d, true);
        map.AddSettlement("Brest", 52.1089d, 23.7175d, true);
        map.AddSettlement("Polotsk", 55.4879d, 28.7856d);
        map.AddSettlement("Lepel", 54.8810d, 28.6990d);
        map.AddSettlement("Borisov", 54.2279d, 28.5050d);
        map.AddSettlement("Krichev", 53.7138d, 31.7143d);
        map.AddSettlement("Berezino", 53.8391d, 28.9879d);
        map.AddSettlement("Rogachev", 53.0934d, 30.0495d);
        map.AddSettlement("Zhlobin", 52.8926d, 30.0240d);
        map.AddSettlement("Slutsk", 53.0152d, 27.5416d);
        map.AddSettlement("Molodechno", 54.3167d, 26.8540d);
        map.AddSettlement("Lida", 53.8833d, 25.2997d);
        map.AddSettlement("Pinsk", 52.1215d, 26.0673d);
        map.AddSettlement("Smolensk", 54.7783d, 32.0509d);
        map.AddSettlement("Osipovichi", 53.2933d, 28.6422d);
        map.AddSettlement("Slonim", 53.0869d, 25.3219d);
        map.AddSettlement("Mozyr", 52.0495d, 29.2456d);
    }

    private static void AddTyphoonInfrastructure(ScenarioMap map)
    {
        map.AddInfrastructureRoute("Moscow-Smolensk Highway", InfrastructureRouteType.Road,
            "Smolensk", "Vyazma", "Gzhatsk", "Mozhaysk", "Moscow");
        map.AddInfrastructureRoute("Moscow-Rzhev Road", InfrastructureRouteType.Road,
            "Rzhev", "Volokolamsk", "Istra", "Moscow");
        map.AddInfrastructureRoute("Leningrad Highway", InfrastructureRouteType.Road,
            "Kalinin", "Klin", "Moscow");
        map.AddInfrastructureRoute("Warsaw Highway", InfrastructureRouteType.Road,
            "Roslavl", "Yukhnov", "Maloyaroslavets", "Podolsk", "Moscow");
        map.AddInfrastructureRoute("Moscow-Oryol Road", InfrastructureRouteType.Road,
            "Oryol", "Tula", "Serpukhov", "Podolsk", "Moscow");

        map.AddInfrastructureRoute("Moscow-Smolensk Railway", InfrastructureRouteType.Railway,
            "Smolensk", "Vyazma", "Mozhaysk", "Moscow");
        map.AddInfrastructureRoute("Riga Railway", InfrastructureRouteType.Railway,
            "Rzhev", "Volokolamsk", "Moscow");
        map.AddInfrastructureRoute("October Railway", InfrastructureRouteType.Railway,
            "Kalinin", "Klin", "Moscow");
        map.AddInfrastructureRoute("Moscow-Kursk Railway", InfrastructureRouteType.Railway,
            "Oryol", "Tula", "Moscow");
        map.AddInfrastructureRoute("Vyazma-Bryansk Railway", InfrastructureRouteType.Railway,
            "Vyazma", "Yukhnov", "Bryansk");

        map.AddInfrastructure("Vnukovo Airfield", InfrastructureSiteType.Airfield, 55.5915d, 37.2615d);
        map.AddInfrastructure("Smolensk North Airfield", InfrastructureSiteType.Airfield, 54.8240d, 32.0250d);
        map.AddInfrastructureAtSettlement("Vyazma Airfield", InfrastructureSiteType.Airfield, "Vyazma");
        map.AddInfrastructureAtSettlement("Kalinin Airfield", InfrastructureSiteType.Airfield, "Kalinin");
        map.AddInfrastructureAtSettlement("Moscow Rail Hub", InfrastructureSiteType.RailHub, "Moscow");
        map.AddInfrastructureAtSettlement("Vyazma Rail Hub", InfrastructureSiteType.RailHub, "Vyazma");
        map.AddInfrastructureAtSettlement("Smolensk Rail Hub", InfrastructureSiteType.RailHub, "Smolensk");
        map.AddInfrastructureAtSettlement("Bryansk Rail Hub", InfrastructureSiteType.RailHub, "Bryansk");
        map.AddInfrastructureAtSettlement("Mozhaysk Supply Depot", InfrastructureSiteType.SupplyDepot, "Mozhaysk");
        map.AddInfrastructureAtSettlement("Kaluga Oka Bridge", InfrastructureSiteType.Bridge, "Kaluga");
        map.AddInfrastructureAtSettlement("Tula Arms Center", InfrastructureSiteType.IndustrialCenter, "Tula");
        map.AddInfrastructureAtSettlement("Moscow Industrial Center", InfrastructureSiteType.IndustrialCenter, "Moscow");
    }

    private static void AddStalingradInfrastructure(ScenarioMap map)
    {
        map.AddInfrastructureRoute("Don Highway", InfrastructureRouteType.Road,
            "Rostov-on-Don", "Novocherkassk", "Millerovo", "Boguchar", "Voronezh");
        map.AddInfrastructureRoute("Chir-Stalingrad Road", InfrastructureRouteType.Road,
            "Millerovo", "Morozovsk", "Surovikino", "Kalach", "Stalingrad");
        map.AddInfrastructureRoute("Kotelnikovo Corridor", InfrastructureRouteType.Road,
            "Rostov-on-Don", "Morozovsk", "Kotelnikovo", "Stalingrad");
        map.AddInfrastructureRoute("Lower Volga Road", InfrastructureRouteType.Road,
            "Saratov", "Kamyshin", "Stalingrad", "Astrakhan");

        map.AddInfrastructureRoute("Stalingrad-Likhaya Railway", InfrastructureRouteType.Railway,
            "Millerovo", "Morozovsk", "Surovikino", "Stalingrad");
        map.AddInfrastructureRoute("Stalingrad-Kotelnikovo Railway", InfrastructureRouteType.Railway,
            "Rostov-on-Don", "Kotelnikovo", "Stalingrad");
        map.AddInfrastructureRoute("Volga Railway", InfrastructureRouteType.Railway,
            "Saratov", "Kamyshin", "Stalingrad");
        map.AddInfrastructureRoute("Stalingrad-Astrakhan Railway", InfrastructureRouteType.Railway,
            "Stalingrad", "Pallasovka", "Astrakhan");

        map.AddInfrastructure("Pitomnik Airfield", InfrastructureSiteType.Airfield, 48.7383d, 44.2450d);
        map.AddInfrastructure("Gumrak Airfield", InfrastructureSiteType.Airfield, 48.7825d, 44.3455d);
        map.AddInfrastructureAtSettlement("Tatsinskaya Airfield", InfrastructureSiteType.Airfield, "Tatsinskaya");
        map.AddInfrastructureAtSettlement("Morozovskaya Airfield", InfrastructureSiteType.Airfield, "Morozovsk");
        map.AddInfrastructureAtSettlement("Stalingrad Rail Hub", InfrastructureSiteType.RailHub, "Stalingrad");
        map.AddInfrastructureAtSettlement("Millerovo Rail Hub", InfrastructureSiteType.RailHub, "Millerovo");
        map.AddInfrastructureAtSettlement("Kotelnikovo Supply Depot", InfrastructureSiteType.SupplyDepot, "Kotelnikovo");
        map.AddInfrastructureAtSettlement("Kalach Don Bridge", InfrastructureSiteType.Bridge, "Kalach");
        map.AddInfrastructureAtSettlement("Stalingrad Volga Ferry", InfrastructureSiteType.Port, "Stalingrad");
        map.AddInfrastructureAtSettlement("Saratov River Port", InfrastructureSiteType.Port, "Saratov");
        map.AddInfrastructureAtSettlement("Astrakhan River Port", InfrastructureSiteType.Port, "Astrakhan");
        map.AddInfrastructureAtSettlement("Stalingrad Tractor Factory", InfrastructureSiteType.IndustrialCenter,
            "Stalingrad");
    }

    private static void AddKurskInfrastructure(ScenarioMap map)
    {
        map.AddInfrastructureRoute("Oryol-Kharkov Highway", InfrastructureRouteType.Road,
            "Oryol", "Maloarkhangelsk", "Kursk", "Oboyan", "Belgorod", "Kharkov");
        map.AddInfrastructureRoute("Kursk-Voronezh Road", InfrastructureRouteType.Road,
            "Kursk", "Tim", "Stary Oskol", "Voronezh");
        map.AddInfrastructureRoute("Kursk-Sumy Road", InfrastructureRouteType.Road,
            "Kursk", "Lgov", "Sudzha", "Sumy");
        map.AddInfrastructureRoute("Belgorod-Sumy Road", InfrastructureRouteType.Road,
            "Belgorod", "Tomarovka", "Grayvoron", "Sumy");

        map.AddInfrastructureRoute("Oryol-Kharkov Railway", InfrastructureRouteType.Railway,
            "Oryol", "Ponyri", "Zolotukhino", "Kursk", "Belgorod", "Kharkov");
        map.AddInfrastructureRoute("Kursk-Lgov Railway", InfrastructureRouteType.Railway,
            "Kursk", "Lgov", "Rylsk");
        map.AddInfrastructureRoute("Kursk-Voronezh Railway", InfrastructureRouteType.Railway,
            "Kursk", "Tim", "Stary Oskol", "Voronezh");
        map.AddInfrastructureRoute("Belgorod-Prokhorovka Railway", InfrastructureRouteType.Railway,
            "Belgorod", "Prokhorovka", "Stary Oskol");

        map.AddInfrastructureAtSettlement("Kursk Airfield", InfrastructureSiteType.Airfield, "Kursk");
        map.AddInfrastructureAtSettlement("Oryol Airfield", InfrastructureSiteType.Airfield, "Oryol");
        map.AddInfrastructureAtSettlement("Belgorod Airfield", InfrastructureSiteType.Airfield, "Belgorod");
        map.AddInfrastructureAtSettlement("Kharkov Airfield", InfrastructureSiteType.Airfield, "Kharkov");
        map.AddInfrastructureAtSettlement("Stary Oskol Airfield", InfrastructureSiteType.Airfield, "Stary Oskol");
        map.AddInfrastructureAtSettlement("Kursk Rail Hub", InfrastructureSiteType.RailHub, "Kursk");
        map.AddInfrastructureAtSettlement("Oryol Rail Hub", InfrastructureSiteType.RailHub, "Oryol");
        map.AddInfrastructureAtSettlement("Belgorod Rail Hub", InfrastructureSiteType.RailHub, "Belgorod");
        map.AddInfrastructureAtSettlement("Ponyri Station", InfrastructureSiteType.RailHub, "Ponyri");
        map.AddInfrastructureAtSettlement("Prokhorovka Station", InfrastructureSiteType.RailHub, "Prokhorovka");
        map.AddInfrastructureAtSettlement("Kursk Supply Depot", InfrastructureSiteType.SupplyDepot, "Kursk");
        map.AddInfrastructureAtSettlement("Kharkov Industrial Center", InfrastructureSiteType.IndustrialCenter,
            "Kharkov");
    }

    private static void AddBagrationInfrastructure(ScenarioMap map)
    {
        map.AddInfrastructureRoute("Moscow-Minsk Highway", InfrastructureRouteType.Road,
            "Smolensk", "Orsha", "Borisov", "Minsk");
        map.AddInfrastructureRoute("Brest-Minsk Highway", InfrastructureRouteType.Road,
            "Brest", "Baranovichi", "Minsk");
        map.AddInfrastructureRoute("Minsk-Vilnius Road", InfrastructureRouteType.Road,
            "Minsk", "Molodechno", "Vilnius");
        map.AddInfrastructureRoute("Mogilev-Minsk Road", InfrastructureRouteType.Road,
            "Mogilev", "Berezino", "Minsk");
        map.AddInfrastructureRoute("Bobruisk-Minsk Road", InfrastructureRouteType.Road,
            "Bobruisk", "Osipovichi", "Minsk");

        map.AddInfrastructureRoute("Moscow-Minsk Railway", InfrastructureRouteType.Railway,
            "Smolensk", "Orsha", "Borisov", "Minsk", "Molodechno", "Vilnius");
        map.AddInfrastructureRoute("Brest-Minsk Railway", InfrastructureRouteType.Railway,
            "Brest", "Baranovichi", "Minsk");
        map.AddInfrastructureRoute("Vitebsk-Orsha Railway", InfrastructureRouteType.Railway,
            "Polotsk", "Vitebsk", "Orsha", "Mogilev", "Zhlobin", "Gomel");
        map.AddInfrastructureRoute("Gomel-Minsk Railway", InfrastructureRouteType.Railway,
            "Gomel", "Zhlobin", "Bobruisk", "Osipovichi", "Minsk");
        map.AddInfrastructureRoute("Lida-Baranovichi Railway", InfrastructureRouteType.Railway,
            "Vilnius", "Lida", "Baranovichi");

        map.AddInfrastructureAtSettlement("Minsk Airfield", InfrastructureSiteType.Airfield, "Minsk");
        map.AddInfrastructureAtSettlement("Vitebsk Airfield", InfrastructureSiteType.Airfield, "Vitebsk");
        map.AddInfrastructureAtSettlement("Orsha Airfield", InfrastructureSiteType.Airfield, "Orsha");
        map.AddInfrastructureAtSettlement("Bobruisk Airfield", InfrastructureSiteType.Airfield, "Bobruisk");
        map.AddInfrastructureAtSettlement("Baranovichi Airfield", InfrastructureSiteType.Airfield, "Baranovichi");
        map.AddInfrastructureAtSettlement("Minsk Rail Hub", InfrastructureSiteType.RailHub, "Minsk");
        map.AddInfrastructureAtSettlement("Orsha Rail Hub", InfrastructureSiteType.RailHub, "Orsha");
        map.AddInfrastructureAtSettlement("Baranovichi Rail Hub", InfrastructureSiteType.RailHub, "Baranovichi");
        map.AddInfrastructureAtSettlement("Zhlobin Rail Hub", InfrastructureSiteType.RailHub, "Zhlobin");
        map.AddInfrastructureAtSettlement("Minsk Supply Depot", InfrastructureSiteType.SupplyDepot, "Minsk");
        map.AddInfrastructureAtSettlement("Borisov Berezina Bridge", InfrastructureSiteType.Bridge, "Borisov");
        map.AddInfrastructureAtSettlement("Bobruisk Berezina Bridge", InfrastructureSiteType.Bridge, "Bobruisk");
        map.AddInfrastructureAtSettlement("Mogilev Dnieper Bridge", InfrastructureSiteType.Bridge, "Mogilev");
        map.AddInfrastructureAtSettlement("Pinsk River Port", InfrastructureSiteType.Port, "Pinsk");
        map.AddInfrastructureAtSettlement("Gomel River Port", InfrastructureSiteType.Port, "Gomel");
        map.AddInfrastructureAtSettlement("Minsk Industrial Center", InfrastructureSiteType.IndustrialCenter,
            "Minsk");
    }

    private static void SetFront(ScenarioMap map, Func<int, float> frontAtY)
    {
        for (int y = 0; y < map.Height; y++)
        {
            float front = frontAtY(y);
            for (int x = 0; x < map.Width; x++)
                if (TerrainRules.IsWalkable(map.TerrainAt(x, y))) map.SetOwner(x, y, x >= front ? 0 : 1);
        }
    }

    private static void AddVerticalRiver(ScenarioMap map, float centerX, float amplitude,
        float frequency, int fordEvery, int fordOffset)
    {
        for (int y = 0; y < map.Height; y++)
        {
            int x = (int)Math.Round(centerX + Math.Sin(y * frequency) * amplitude);
            TerrainType water = (y + fordOffset) % fordEvery < 2 ? TerrainType.Ford : TerrainType.River;
            for (int thickness = 0; thickness < 2; thickness++) map.SetTerrain(x + thickness, y, water);
        }
    }

    private static void AddTerrainBand(ScenarioMap map, int minX, int maxX, int minY, int maxY,
        TerrainType terrain, int threshold)
    {
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
            if (map.TerrainAt(x, y) != TerrainType.River && map.TerrainAt(x, y) != TerrainType.Ford &&
                map.TerrainAt(x, y) != TerrainType.Water && Hash(x, y, 991) % 100 < threshold)
                map.SetTerrain(x, y, terrain);
    }

    private static void AddMarshes(ScenarioMap map)
    {
        for (int x = 49; x < 91; x++)
        for (int y = 30; y < 68; y++)
            if (Hash(x, y, 1944) % 100 < 12) map.SetTerrain(x, y, TerrainType.Water);
    }

    private static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
            value = (value ^ (value >> 13)) * 1274126177u;
            return value ^ (value >> 16);
        }
    }
}
