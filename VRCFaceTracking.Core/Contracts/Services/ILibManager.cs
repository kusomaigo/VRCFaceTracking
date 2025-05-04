using System.Collections.ObjectModel;

namespace VRCFaceTracking.Core.Contracts.Services;

public interface ILibManager
{
    //public ObservableCollection<ModuleMetadata> LoadedModulesMetadata { get; set; }
    public ObservableCollection<ModuleLiveData> LoadedModulesLiveData { get; set; }
    public void Initialize();
    void TeardownAllAndResetAsync();
}