using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Validation;

namespace VRCFaceTracking.Core.Models;

public partial class OscTarget : ObservableValidator, IOscTarget
{
    [ObservableProperty] private bool _isConnected;

    [ObservableProperty] [property: SavedSetting("OSCInPort", 9001)]

    private int _inPort;

    [ObservableProperty][property: SavedSetting("OSCVisualInPort", 69696)]

    private int _visualInPort;

    [ObservableProperty] [property: SavedSetting("OSCOutPort", 9000)]

    private int _outPort;

    [ObservableProperty] [property: SavedSetting("UseOSCQuery", true)]

    private bool _useOscQuery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InPort))]
    [NotifyPropertyChangedFor(nameof(VisualInPort))]
    [NotifyPropertyChangedFor(nameof(OutPort))]
    [NotifyPropertyChangedFor(nameof(UseOscQuery))]
    [property: SavedSetting("OSCAddress", "127.0.0.1")]
    [ValidIpAddress]
    private string _destinationAddress;

    public OscTarget(ILocalSettingsService localSettingsService)
    {
        PropertyChanged += (_, _) => localSettingsService.Save(this);
    }
}
