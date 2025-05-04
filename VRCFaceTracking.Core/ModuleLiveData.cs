using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using static VRCFaceTracking.ModuleMetadata;

namespace VRCFaceTracking.Core;
public partial class ModuleLiveData : ObservableObject
{
    public delegate void ActiveChange(bool state);
    public ActiveChange OnActiveChange;
    public ModuleMetadata LoadedModuleMetadata { get; set; }

    [ObservableProperty]
    public UpdateInfo moduleUpdateInfo;

    private bool _active;

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            OnActiveChange?.Invoke(value);
        }
    }

    public ModuleLiveData(ModuleMetadata md, UpdateInfo ui) 
    {
        LoadedModuleMetadata = md;
        ModuleUpdateInfo = ui;
    }
}
