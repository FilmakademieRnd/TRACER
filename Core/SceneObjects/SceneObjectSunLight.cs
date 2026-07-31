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
using System.Xml.Serialization;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER directional light object as a specialisation of the light object
    //!
    public class SceneObjectSunLight : SceneObjectDirectionalLight
    {
        //!
        //! The gegraphical longitude to calculate the sun position.
        //!
        public Parameter<int> m_longitude { get; private set; }
        //!
        //! The gegraphical latitude to calculate the sun position.
        //!
        public Parameter<int> m_latitude { get; private set; }
        public DateTime m_date { get; private set; }
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_minute;
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_hour;
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_day;
        //!
        //! The the minunte of the time used to calculate the sun position.
        //!
        private Parameter<int> m_month;

        //!
        //! Factory to create a new SceneObject and do it's initialisation.
        //! Use this function instead GameObject.AddComponen<>!
        //!
        //! @param gameObject The gameObject the new SceneObject will be attached to.
        //! @sceneID The scene ID for the new SceneObject.
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

            m_longitude.hasChanged += updateSunPosition;
            m_latitude.hasChanged += updateSunPosition;
            m_minute.hasChanged += updateSunPosition;
            m_hour.hasChanged += updateSunPosition;
            m_day.hasChanged += updateSunPosition;
            m_month.hasChanged += updateSunPosition;

            updateSunPosition(this, 0);
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();

        }

        //!
        //! canlculate the suns position and update the directional light.
        //! @param   sender     Object calling the update function
        //! @param   a          new color value
        //!
        private void updateSunPosition(object sender, int value)
        {
            try
            {
                m_date = new DateTime(1900, m_month.value, m_day.value, m_hour.value, m_minute.value, 0);

                float declination = -23.45f * Mathf.Deg2Rad * Mathf.Cos(0.9863f * Mathf.Deg2Rad * (m_date.DayOfYear + 10));
                float floatMinute = 60f * (-0.171f * Mathf.Sin(0.0337f * m_date.DayOfYear + 0.465f) - 0.1299f * Mathf.Sin(0.01787f * m_date.DayOfYear - 0.168f));
                float sinLatitute = Mathf.Sin(m_latitude.value * Mathf.Deg2Rad); // breite
                float sinDeclination = Mathf.Sin(declination);
                float cosLatitute = Mathf.Cos(m_latitude.value * Mathf.Deg2Rad);
                float cosDeclination = Mathf.Cos(declination);

                float hourAngle = 15f * (m_hour.value + m_minute.value / 60f - (15f - m_longitude.value) / 15.0f - 12f + floatMinute / 60f);
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
                color.value = new Color(1f, MathF.Exp(-0.05f * m), MathF.Exp(-0.18f * m));
                _lock = false;
                intensity.value = sinSunHeight;

                //Debug.Log("Sun color: " + sunColor + "| Sin Sun Height: " + sinSunHeight);
                //Debug.Log("Sun height: " + sunHeight + "| Sun direction: " + sunDirection);
            }
            catch (Exception e)
            {
                Helpers.Log(e.ToString(), Helpers.logMsgType.WARNING);
                return;
            }
        }

    }
}
