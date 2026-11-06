/*
-----------------------------------------------------------------------------------
TRACER FOUNDATION -
Toolset for Realtime Animation, Collaboration & Extended Reality

Copyright (c) 2024 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Labs
https://research.animationsinstitut.de/tracer 
https://github.com/FilmakademieRnd/TRACER

TRACER FOUNDATION is a development by Filmakademie Baden-Wuerttemberg,
Animationsinstitut R&D Labs in the scope of the EU funded project
MAX-R (101070072) and funding on the own behalf of Filmakademie Baden-Wuerttemberg.
Former EU projects Dreamspace (610005) and SAUCE (780470) have inspired the
TRACER FOUNDATION development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program;
if not go to https://opensource.org/licenses/MIT
-----------------------------------------------------------------------------------
*/

//! @file "GPSModule.cs"
//! @brief implementation of TRACER device specific gps data to InputManager
//! @author Thomas Krüger
//! @version 1
//! @date 06.11.2026
//! @changed doxygen style documentation

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace tracer{
    //!
    //! implementation of TRACER GPS capacity
    //!
    public class GPSModule : InputManagerModule{

        #region Settings
        //!
        //! The service accuracy you want to use (in meters)
        //! Determines the accuracy of the device's last location coordinates. 
        //! values > 500 don't require the device to use its GPS chip and thus save battery power. 
        //! Lower values like 5-10 provide the best accuracy but require the GPS chip and thus use more battery power. 
        //! The default value is 10 meters.
        //!
        private const float DESIRED_ACCURACY_M = 10;
        //!
        //! The minimum distance, in meters, that the device must move laterally before Unity updates Input.location. 
        //! Higher values like 500 produce fewer updates and are less resource intensive to process. 
        //! The default is 10 meters.
        //!
        private const float UPDATE_DISTANCE_M = 10f;
        //!
        //! default latitude value we use as fallback (set to Ludwigsburg: 48.8894f) if hardware is not present or enabled
        //!
        private const float DEFAULT_LAT = 48.8894f;
        //!
        //! default longitude value we use as fallback (set to Ludwigsburg: 9.1943f) if hardware is not present or enabled
        //!
        private const float DEFAULT_LON = 9.1943f;
        //!
        //! broadcast frequency of update events in seconds
        //!
        private const float LOCATION_UPDATE_EVERY = 1f;

        #endregion

        //!
        //! reference of currently stacked events raised from the InputManager (onGPSDemandChangedEvent)
        //!
        private int _continuousDemandCount = 0;
        //!
        //! reference of whether we are already started our gps service and are tracking our location
        //!
        private bool _isLocating = false;
        //!
        //! coroutine reference for starting and stoping it instead of using a name (ContinuousLoopRoutine)
        //!
        private Coroutine _gpsLoopRoutine;
        //!
        //! struct for our gps info with minimal functionality to convert double timestamp into specific int values
        //!
        private struct TimeDataStruct
        {
            public readonly int minute;
            public readonly int hour;
            public readonly int day;
            public readonly int month;
            
            public TimeDataStruct(double _gpsTimestamp = 0)
            {
                // Populate the time fields
                DateTime targetTime;

                if (_gpsTimestamp > 0)
                {
                    // 1. BEST PRACTICE: Convert Unity's hardware GPS timestamp (Unix Epoch seconds since 1970)
                    // This represents the exact moment the satellite sent the coordinate, regardless of delays.
                    DateTime epochStart = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    targetTime = epochStart.AddSeconds(_gpsTimestamp);
                }
                else
                {
                    // 2. FALLBACK: Use the device's current clock.
                    // We use UtcNow instead of Now so your solar formulas don't break due to Daylight Savings or Time Zones!
                    targetTime = DateTime.UtcNow;
                }

                minute = targetTime.Minute;
                hour = targetTime.Hour;
                day = targetTime.Day;
                month = targetTime.Month;
            }
        }

        //!
        //! Constructor.
        //!
        //! @param name Name of this module.
        //! @param _core Reference to the TRACER _core.
        //!
        public GPSModule(string name, Manager manager) : base(name, manager) {}

        //! 
        //! Init m_callback. Add listener at InputManager and register a handler to use specific call to start gps
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e){
            manager.onGPSDemandChangedEvent += HandleGPSDemandEvent;
            manager.RegisterHandler<InputManager.StartGPSCommand>(startGPS);
        }

        private void startGPS(InputManager.StartGPSCommand c){
            //can this be started from within this function?
            // _continuousDemandCount++;
            // if (_continuousDemandCount == 1 && !_isLocating){
            //     _gpsLoopRoutine = core.StartCoroutine(ContinuousLoopRoutine());
            // }
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose(){
            base.Dispose();
            manager.UnregisterHandler<InputManager.StartGPSCommand>();
            manager.onGPSDemandChangedEvent -= HandleGPSDemandEvent;
            StopGPSHardware(false);
        }

        //!
        //! This method responds a gps demand from any module via an InputManager event
        //! will trigger a single gps update, start/stop gps continous update coro
        //!
        //! @param sender where does it got called from
        //! @param demandType the type of gps signal request
        //!
        private void HandleGPSDemandEvent(object sender, InputManager.GPSDemandType demandType){
            switch (demandType){
                case InputManager.GPSDemandType.OneShot:
                    // If we are already running continuous, just reply immediately with current data!
                    if (_isLocating && Input.location.status == LocationServiceStatus.Running){
                        BroadcastCurrentLocation(InputManager.InputState.Ended);
                    }else{
                        // Otherwise, wake up just long enough to get one fix
                        core.StartCoroutine(OneShotRoutine());
                    }
                    break;
                case InputManager.GPSDemandType.StartContinuous:
                    _continuousDemandCount++;
                    if (_continuousDemandCount == 1 && !_isLocating){
                        _gpsLoopRoutine = core.StartCoroutine(ContinuousLoopRoutine());
                    }
                    break;

                case InputManager.GPSDemandType.StopContinuous:
                    _continuousDemandCount = Mathf.Max(0, _continuousDemandCount - 1);
                    if (_continuousDemandCount == 0 && _gpsLoopRoutine != null){
                        core.StopCoroutine(_gpsLoopRoutine);
                        StopGPSHardware(true);
                    }
                    break;
            }
        }

        // --- COROUTINES & HARDWARE ---
        //!
        //! try to initialize the gps hardware, if not already done
        //!
        private IEnumerator InitializeGPSHardware(){
            if (_isLocating) 
                yield break;
            
            _isLocating = true;

            if (!Input.location.isEnabledByUser){
                #if UNITY_EDITOR
                Helpers.Log("GPS not enabled, faking in editor.", Helpers.logMsgType.WARNING);
                #else
                Helpers.Log("GPS not enabled by user, feature will not be available.", Helpers.logMsgType.WARNING);
                #endif
                yield break;
            }

            Input.location.Start(DESIRED_ACCURACY_M, UPDATE_DISTANCE_M);

            int maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0){
                yield return new WaitForSeconds(1);
                maxWait--;
            }
        }

        //!
        //! execute initializeing, wait for a valid update to be sent and stop gps loop again
        //!
        private IEnumerator OneShotRoutine(){
            yield return core.StartCoroutine(InitializeGPSHardware());

            //one shot will only ever call "Ended", no Start or Ongoing
            BroadcastCurrentLocation(InputManager.InputState.Ended);
            
            if (_continuousDemandCount == 0) 
                StopGPSHardware(false);
        }
        //!
        //! execute a continous gps update and broadcast
        //!
        private IEnumerator ContinuousLoopRoutine(){
            yield return core.StartCoroutine(InitializeGPSHardware());

            BroadcastCurrentLocation(InputManager.InputState.Started);
            yield return new WaitForSeconds(Mathf.Max(LOCATION_UPDATE_EVERY, Time.deltaTime));
            
            while (_continuousDemandCount > 0){
                BroadcastCurrentLocation(InputManager.InputState.Ongoing);
                yield return new WaitForSeconds(Mathf.Max(LOCATION_UPDATE_EVERY, Time.deltaTime));

            }
            StopGPSHardware(true);
        }

        //!
        //! stop the gps (unity input)
        //! also sends and update if we were locating
        //!
        private void StopGPSHardware(bool _broadcastEnded){
            if (_isLocating){
                if(_broadcastEnded)
                    BroadcastCurrentLocation(InputManager.InputState.Ended);
                
                Input.location.Stop();
                _isLocating = false;
            }
        }

        //!
        //! broadcast the current gps location as InputManager event to be raised
        //! if hardeware failed, send a fallback
        //!
        //! @param _state state of our "input" (started, ongoing, ended)
        //!
        private void BroadcastCurrentLocation(InputManager.InputState _state){
            if (Input.location.status == LocationServiceStatus.Running){
                var info = Input.location.lastData;
                TimeDataStruct timeData = new TimeDataStruct(info.timestamp);
                manager.RaiseGPS(this, new InputManager.GPSEventArgs(
                    InputManager.InputLevel.Primary, _state, 
                    info.latitude, info.longitude, info.altitude, info.horizontalAccuracy, true, 
                    timeData.minute, timeData.hour, timeData.day, timeData.month));
            }
            else{
                // Hardware failed or timed out: Broadcast fallback coordinates so the app doesn't break
                TimeDataStruct timeData = new TimeDataStruct(0);
                manager.RaiseGPS(this, new InputManager.GPSEventArgs(
                    InputManager.InputLevel.Primary, _state,
                    DEFAULT_LAT, DEFAULT_LON, 0, DESIRED_ACCURACY_M, false,
                    timeData.minute, timeData.hour, timeData.day, timeData.month));
            }
        }
    }

}
