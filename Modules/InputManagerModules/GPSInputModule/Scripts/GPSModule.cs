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
//! @version 0
//! @date 19.05.2026

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace tracer{
    //!
    //! implementation of TRACER attitude sensore navigation
    //!
    public class GPSModule : InputManagerModule{

        //Settings
        // The service accuracy you want to use, in meters. 
        // This determines the accuracy of the device's last location coordinates. 
        // Higher values like 500 don't require the device to use its GPS chip and thus save battery power. 
        // Lower values like 5-10 provide the best accuracy but require the GPS chip and thus use more battery power. 
        // The default value is 10 meters.
        private const float DESIRED_ACCURACY_M = 10;
        // The minimum distance, in meters, that the device must move laterally before Unity updates Input.location. 
        // Higher values like 500 produce fewer updates and are less resource intensive to process. 
        // The default is 10 meters.
        private const float UPDATE_DISTANCE_M = 10f;
        private const float DEFAULT_LAT = 48.8894f; // Fallback (Ludwigsburg) if hardware fails/isn't present
        private const float DEFAULT_LON = 9.1943f;
        private const float LOCATION_UPDATE_EVERY = 1f; //broadcast every seconds (0 means time.deltatime as wait)

        private int _continuousDemandCount = 0;
        private bool _isLocating = false;
        private Coroutine _gpsLoopRoutine;
        private InputManager.GPSEventArgs gpsInputData;
        private InputManager.GPSDataStruct gpsData;

        //!
        //! Constructor.
        //!
        //! @param name Name of this module.
        //! @param _core Reference to the TRACER _core.
        //!
        public GPSModule(string name, Manager manager) : base(name, manager){}

        //! 
        //! Init m_callback
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        protected override void Init(object sender, EventArgs e){
            //created once, only the state will ever change here!
            gpsInputData = InputTracker.ToArgs<InputManager.GPSEventArgs>(
                InputManager.InputLevel.Primary,
                InputManager.InputState.Started,
                Vector2.zero,
                Vector2.zero
            );

            InputManager.OnGPSDemandChanged += HandleGPSDemandEvent;
        }

        //!
        //! Destructor, cleaning up event registrations. 
        //!
        public override void Dispose(){
            base.Dispose();
            InputManager.OnGPSDemandChanged -= HandleGPSDemandEvent;
            StopGPSHardware(false);
        }

        

        private void HandleGPSDemandEvent(InputManager.GPSDemandType demandType){
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

        private IEnumerator OneShotRoutine(){
            yield return core.StartCoroutine(InitializeGPSHardware());

            //one shot will only ever call "Ended", no Start or Ongoing
            BroadcastCurrentLocation(InputManager.InputState.Ended);
            
            if (_continuousDemandCount == 0) 
                StopGPSHardware(false);
        }

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


        private void StopGPSHardware(bool _broadcastEnded){
            if (_isLocating){
                if(_broadcastEnded)
                    BroadcastCurrentLocation(InputManager.InputState.Ended);
                
                Input.location.Stop();
                _isLocating = false;
            }
        }

        private void BroadcastCurrentLocation(InputManager.InputState _state){
            gpsInputData.State = _state;
            if (Input.location.status == LocationServiceStatus.Running){
                var info = Input.location.lastData;
                gpsData = new InputManager.GPSDataStruct(info.latitude, info.longitude, info.altitude, info.horizontalAccuracy, true, info.timestamp){};
            }else{
                // Hardware failed or timed out: Broadcast fallback coordinates so the app doesn't break
                gpsData = new InputManager.GPSDataStruct(DEFAULT_LAT, DEFAULT_LON, DESIRED_ACCURACY_M, 0f, false, 0){};
            }
            gpsInputData.GPSData = gpsData;

            manager.RaiseGPS(this, gpsInputData);
        }
    }

}
