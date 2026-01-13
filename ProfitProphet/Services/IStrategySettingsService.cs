using ProfitProphet.Models.Strategies;
using System.Collections.Generic;

namespace ProfitProphet.Services
{
    public interface IStrategySettingsService
    {
        // Visszaadja a mentett profilokat
        List<StrategyProfile> LoadProfiles();

        // Elmenti az adott profilt (ha létezik frissíti, ha nem hozzáadja)
        void SaveProfile(StrategyProfile profile);

        // (Opcionális: Törlés funkció)
        void DeleteProfile(StrategyProfile profile);
    }
}
