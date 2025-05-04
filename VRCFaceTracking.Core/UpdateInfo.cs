using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Types;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VRCFaceTracking.Core;
public partial class UpdateInfo : ObservableObject
{
    [ObservableProperty]
    private int updateRate;

    private int _updateRate;

    private long _lastUpdateInterval;
    private int _lastUpdateChangesNum;

    private readonly Stopwatch _sw;
    private readonly ExtTrackingModule _module;

    private readonly UnifiedTrackingData _pastData;

    public UpdateInfo(ExtTrackingModule module) 
    {
        _sw = new Stopwatch();
        _module = module;
        _updateRate = 0;
        _lastUpdateInterval = 0;
        _lastUpdateChangesNum = 0;

        _pastData = new UnifiedTrackingData(); 

        _sw.Reset();
        _sw.Start();
    }

    public void registerUpdate()
    {
        if (_module == null)
        {
            return;
        }
        // register a new time interval 
        //_sw.Stop();
        //_lastUpdateInterval = _sw.ElapsedMilliseconds;
        //_sw.Reset();
        // compare data

        //_lastUpdateChangesNum = 0;

        // calculate lastUpdateChangesNum
        // only count for the part of the face that this module is in charge of
        if (_module.ModuleInformation.UsingEye)
        {
            // check eye data
            Vector2 leftGazeDiff = UnifiedTracking.Data.Eye.Left.Gaze - _pastData.Eye.Left.Gaze;
            Vector2 rightGazeDiff = UnifiedTracking.Data.Eye.Left.Gaze - _pastData.Eye.Left.Gaze;
            _lastUpdateChangesNum += (leftGazeDiff.x != 0 || leftGazeDiff.y != 0) ? 1 : 0;
            _lastUpdateChangesNum += (rightGazeDiff.x != 0 || rightGazeDiff.y != 0) ? 1 : 0;
            _lastUpdateChangesNum += UnifiedTracking.Data.Eye.Left.Openness - _pastData.Eye.Left.Openness != 0 ? 1 : 0;
            _lastUpdateChangesNum += UnifiedTracking.Data.Eye.Left.PupilDiameter_MM - _pastData.Eye.Left.PupilDiameter_MM != 0 ? 1 : 0;
            _lastUpdateChangesNum += UnifiedTracking.Data.Eye.Right.Openness - _pastData.Eye.Right.Openness != 0 ? 1 : 0;
            _lastUpdateChangesNum += UnifiedTracking.Data.Eye.Right.PupilDiameter_MM - _pastData.Eye.Right.PupilDiameter_MM != 0 ? 1 : 0;
        }

        if (_module.ModuleInformation.UsingExpression)
        {
            for (int i = 0; i < UnifiedTracking.Data.Shapes.Length; i++)
            {
                _lastUpdateChangesNum += UnifiedTracking.Data.Shapes[i].Weight - _pastData.Shapes[i].Weight != 0 ? 1 : 0;
            }
        }

        // head is kind of it's own thing because it wasn't added to module data, so it's checked regardless for now...
        _lastUpdateChangesNum += UnifiedTracking.Data.Head.HeadYaw - _pastData.Head.HeadYaw != 0 ? 1 : 0;
        _lastUpdateChangesNum += UnifiedTracking.Data.Head.HeadPitch - _pastData.Head.HeadPitch != 0 ? 1 : 0;
        _lastUpdateChangesNum += UnifiedTracking.Data.Head.HeadRoll - _pastData.Head.HeadRoll != 0 ? 1 : 0;
        _lastUpdateChangesNum += UnifiedTracking.Data.Head.HeadPosX - _pastData.Head.HeadPosX != 0 ? 1 : 0;
        _lastUpdateChangesNum += UnifiedTracking.Data.Head.HeadPosY - _pastData.Head.HeadPosY != 0 ? 1 : 0;
        _lastUpdateChangesNum += UnifiedTracking.Data.Head.HeadPosZ - _pastData.Head.HeadPosZ != 0 ? 1 : 0;

        // copy over UnifiedTrackingData
        _pastData.CopyPropertiesOf(UnifiedTracking.Data);

        // update the update rate
        // actually we can't do that because only UI thread should cause UI changing calls
        //_updateRate = (int)(_lastUpdateChangesNum / (_lastUpdateInterval / 1000.0));

        // restart the stopwatch
        //_sw.Start();
    }

    //protected void OnPropertyChanged([CallerMemberName] string name = null)
    //{
    //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    //}

    public void GetLatestUpdateRate()
    {
        _sw.Stop();
        _lastUpdateInterval = _sw.ElapsedMilliseconds;
        _sw.Reset();
        //UpdateRate = _updateRate;
        UpdateRate = (int)(_lastUpdateChangesNum / (_lastUpdateInterval / 1000.0));
        _lastUpdateChangesNum = 0;
        _sw.Start();
    }
}
