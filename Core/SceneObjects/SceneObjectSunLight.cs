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

//! @file "SceneObjectPointLight.cs"
//! @brief implementation SceneObjectSunLight as a specialisation of the light object.
//! @author Simon Spielmann
//! @author Jonas Trottnow
//! @version 0
//! @date 03.07.2026

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER directional light object as a specialisation of the light object
    //!
    public class SceneObjectSunLight : SceneObjectDirectionalLight
    {
        //!
        //! The geographical longitude to calculate the sun position.
        //!
        public Parameter<int> m_longitude { get; private set; }
        //!
        //! The geographical latitude to calculate the sun position.
        //!
        public Parameter<int> m_latitude { get; private set; }
        public DateTime m_date { get; private set; }
        //!
        //! The minute of the time used to calculate the sun position.
        //!
        private Parameter<int> m_minute;
        //!
        //! The hour of the time used to calculate the sun position.
        //!
        private Parameter<int> m_hour;
        //!
        //! The day of the time used to calculate the sun position.
        //!
        private Parameter<int> m_day;
        //!
        //! The month of the time used to calculate the sun position.
        //!
        private Parameter<int> m_month;

        //!
        //! Stores active animation coroutines per parameter to prevent overlapping transitions.
        //!
        private readonly Dictionary<Parameter<int>, Coroutine> m_activeAnimations = new Dictionary<Parameter<int>, Coroutine>();
        //!
        //! Stores smooth float values for each parameter to ensure stutter-free sub-integer movement.
        //!
        private readonly Dictionary<Parameter<int>, float> m_smoothValues = new Dictionary<Parameter<int>, float>();
        //!
        //! Flag indicating whether a parameter change was triggered internally by an animation step.
        //!
        private bool m_isAnimatingStep = false;
        //!
        //! Flag indicating that the sun position needs to be recalculated once in the current frame.
        //!
        private bool m_isDirty = false;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @param sceneID The scene ID for the new SceneObject.
        //!
        public static new SceneObjectSunLight Attach(GameObject gameObject, byte sceneID)
        {
            SceneObjectSunLight obj = gameObject.AddComponent<SceneObjectSunLight>();
            obj.Init(sceneID);

            return obj;
        }

        // Start is called before the first frame update
        public override void Awake()
        {
            base.Awake();

            m_longitude = new Parameter<int>(10, "Longitude", this);
            m_latitude = new Parameter<int>(10, "Latitude", this);
            m_minute = new Parameter<int>(10, "Minute", this);
            m_hour = new Parameter<int>(10, "Hour", this);
            m_day = new Parameter<int>(10, "Day", this);
            m_month = new Parameter<int>(10, "Month", this);

            // Initialize smooth float values with initial defaults
            m_smoothValues[m_longitude] = m_longitude.value;
            m_smoothValues[m_latitude] = m_latitude.value;
            m_smoothValues[m_minute] = m_minute.value;
            m_smoothValues[m_hour] = m_hour.value;
            m_smoothValues[m_day] = m_day.value;
            m_smoothValues[m_month] = m_month.value;

            m_hour.hasChanged += checkHour;

            m_longitude.hasChanged += onParameterChanged;
            m_latitude.hasChanged += onParameterChanged;
            m_minute.hasChanged += onParameterChanged;
            m_hour.hasChanged += onParameterChanged;
            m_day.hasChanged += onParameterChanged;
            m_month.hasChanged += onParameterChanged;

            updateSunPosition();
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();

            // Recalculate sun position at most ONCE per frame if any value changed
            if (m_isDirty)
            {
                m_isDirty = false;
                updateSunPosition();
            }
        }

        //!
        //! Clamps the hour parameter to a valid range (0-23) when modified externally.
        //! @param sender Object calling the validation function
        //! @param value  New hour value
        //!
        private void checkHour(object sender, int value)
        {
            if (m_isAnimatingStep) return;

            Parameter<int> param = sender as Parameter<int>;
            if (value > 23) param.value = 23;
            if (value < 0) param.value = 0;
        }

        //!
        //! Intercepts parameter changes to smoothly interpolate from the previous value to the new target.
        //! @param sender   Parameter object that was modified
        //! @param newValue Target value assigned to the parameter
        //!
        private void onParameterChanged(object sender, int newValue)
        {
            if (m_isAnimatingStep)
            {
                m_isDirty = true;
                return;
            }

            Parameter<int> param = sender as Parameter<int>;
            if (param == null) return;

            if (param == m_hour)
            {
                newValue = Mathf.Clamp(newValue, 0, 23);
            }

            if (m_activeAnimations.TryGetValue(param, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }

            m_activeAnimations[param] = StartCoroutine(AnimateParameterRoutine(param, newValue, 0.4f));
        }

        //!
        //! Coroutine that smoothly interpolates a parameter value over a given duration.
        //! @param param       The parameter to animate
        //! @param targetValue The final value to reach
        //! @param duration    Duration of the interpolation in seconds
        //!
        private IEnumerator AnimateParameterRoutine(Parameter<int> param, int targetValue, float duration)
        {
            float startValue = m_smoothValues.ContainsKey(param) ? m_smoothValues[param] : targetValue;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / duration));
                float smoothVal = Mathf.Lerp(startValue, targetValue, t);

                m_smoothValues[param] = smoothVal;
                m_isDirty = true;

                // Only update the integer parameter when the rounded value actually changes
                int roundedVal = Mathf.RoundToInt(smoothVal);
                if (param.value != roundedVal)
                {
                    SetParameterStep(param, roundedVal);
                }

                yield return null;
            }

            m_smoothValues[param] = targetValue;
            SetParameterStep(param, targetValue);
            m_isDirty = true;
            m_activeAnimations.Remove(param);
        }

        //!
        //! Updates a parameter's integer value internally without restarting the animation.
        //! @param param The parameter to update
        //! @param val   The interpolated step value
        //!
        private void SetParameterStep(Parameter<int> param, int val)
        {
            m_isAnimatingStep = true;
            param.value = val;
            m_isAnimatingStep = false;
        }

        //!
        //! Calculate the sun's position and update the directional light using smooth float values.
        //!
        private void updateSunPosition()
        {
            try
            {
                float smoothMonth = m_smoothValues[m_month];
                float smoothDay = m_smoothValues[m_day];
                float smoothHour = m_smoothValues[m_hour];
                float smoothMinute = m_smoothValues[m_minute];
                float smoothLat = m_smoothValues[m_latitude];
                float smoothLon = m_smoothValues[m_longitude];

                int intMonth = Mathf.Clamp(Mathf.RoundToInt(smoothMonth), 1, 12);
                int maxDays = DateTime.DaysInMonth(1900, intMonth);
                int safeDay = Mathf.Clamp(Mathf.RoundToInt(smoothDay), 1, maxDays);

                m_date = new DateTime(1900, intMonth, safeDay, m_hour.value, m_minute.value, 0);

                // Include fractional day progress for smooth seasonal transitions
                float dayOfYear = m_date.DayOfYear + (smoothDay - Mathf.Round(smoothDay));

                float declination = -23.45f * Mathf.Deg2Rad * Mathf.Cos(0.9863f * Mathf.Deg2Rad * (dayOfYear + 10f));
                float floatMinute = 60f * (-0.171f * Mathf.Sin(0.0337f * dayOfYear + 0.465f) - 0.1299f * Mathf.Sin(0.01787f * dayOfYear - 0.168f));
                float sinLatitute = Mathf.Sin(smoothLat * Mathf.Deg2Rad);
                float sinDeclination = Mathf.Sin(declination);
                float cosLatitute = Mathf.Cos(smoothLat * Mathf.Deg2Rad);
                float cosDeclination = Mathf.Cos(declination);

                // Use smoothHour and smoothMinute so the sun glides seamlessly instead of jumping in integer steps
                float hourAngle = 15f * (smoothHour + smoothMinute / 60f - (15f - smoothLon) / 15.0f - 12f + floatMinute / 60f);
                float cosHourAngle = Mathf.Cos(hourAngle * Mathf.Deg2Rad);
                float sinSunHeight = sinLatitute * sinDeclination + cosLatitute * cosDeclination * cosHourAngle;
                float sunHeight = Mathf.Asin(sinSunHeight) * Mathf.Rad2Deg;
                float sunDirection = Mathf.Acos(-(sinLatitute * sinSunHeight - sinDeclination) / (cosLatitute * Mathf.Sin(Mathf.Acos(sinSunHeight)))) * Mathf.Rad2Deg;

                if (hourAngle > 0)
                    sunDirection = 360f - sunDirection;

                _lock = true;
                rotation.value = Quaternion.Euler(sunHeight, sunDirection, 0.0f);

                if (sinSunHeight < 0.01f) sinSunHeight = 0.01f;
                float m = 1f / sinSunHeight;
                Color sunColor = new Color(1f, MathF.Exp(-0.05f * m), MathF.Exp(-0.18f * m));
                color.value = sunColor;
                _lock = false;
                intensity.value = sinSunHeight;
            }
            catch (Exception e)
            {
                Helpers.Log(e.ToString(), Helpers.logMsgType.WARNING);
            }
        }
    }
}
