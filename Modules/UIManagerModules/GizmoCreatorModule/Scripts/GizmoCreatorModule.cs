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

//! @file "GizmoCreatorModule.cs"
//! @brief Implementation of the TRACER GizmoCreatorModule, creating line based gizmo objects.
//! @author Simon Spielmann
//! @version 0
//! @date 18.02.2022

using System;
using System.Collections.Generic;
using UnityEngine;

namespace tracer
{
    //!
    //! Implementation of the TRACER GizmoCreatorModule, creating line based gizmo objects.
    //!
    public class GizmoCreatorModule : UIManagerModule
    {
        //!
        //! The list of created gizmos.
        //!
        private List<VPETGizmo> m_gizmos;
        //!
        //! Stored positions for a line.
        //!
        private static Vector3[] m_linePos = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 5.0f)
        };
        //!
        //! Stored positions for a rectangle.
        //!
        private static Vector3[] m_rectPos = new Vector3[]
        {
            new Vector3( -0.5f,-0.5f, 0.0f ),
            new Vector3(  0.5f,-0.5f, 0.0f ),
            new Vector3(  0.5f, 0.5f, 0.0f ),
            new Vector3( -0.5f, 0.5f, 0.0f )
        };
        //!
        //! Stored positions for a cone.
        //!
        private static Vector3[] m_conePos = new Vector3[]
        {
            new Vector3(  0.0f, 0.0f, 0.0f ),
            new Vector3( -0.5f,-0.5f, 1.0f ),

            new Vector3(  0.0f, 0.0f, 0.0f ),
            new Vector3(  0.5f,-0.5f, 1.0f ),

            new Vector3(  0.0f, 0.0f, 0.0f ),
            new Vector3(  0.5f, 0.5f, 1.0f ),

            new Vector3(  0.0f, 0.0f, 0.0f ),
            new Vector3( -0.5f, 0.5f, 1.0f )
        };
        //!
        //! Stored positions for a circle.
        //!
        private static Vector3[] m_circlePos;
        //!
        //! Stored positions for a circle.
        //!
        private static Vector3[] m_eclipticPos;
        //!
        //! List storing event connections for releasing them before gizmos will be deleted.
        //!
        private List<Tuple<SceneObject, EventHandler<AbstractParameter>>> m_ParameterEventHandlers;
        //!
        //! 
        //!
        private List<Tuple<Parameter<Color>, EventHandler<Color>>> m_eventHandlersColor;
        
        private bool _negative = false;

        //!
        //! Constructor
        //! @param name Name of this module
        //! @param _core Reference to the TRACER _core
        //!
        public GizmoCreatorModule(string name, Manager manager) : base(name, manager)
        {
            m_ParameterEventHandlers = new List<Tuple<SceneObject, EventHandler<AbstractParameter>>>();
            m_eventHandlersColor = new List<Tuple<Parameter<Color>, EventHandler<Color>>>();
            m_gizmos = new List<VPETGizmo>();
            m_circlePos = new Vector3[32];
            m_eclipticPos = new Vector3[32];

            // creating points for a circle
            for (int i=0; i<m_circlePos.Length; i++)
            {
                float step = (Mathf.PI * 2.0f * i) / m_circlePos.Length;
                float x = Mathf.Sin(step);
                float y = Mathf.Cos(step);
                m_circlePos[i] = new Vector3(x * 0.5f, y * 0.5f, 0f);
            }

        }

        //!
        //! Init Function, connecting module with celsction changed event.
        //!
        protected override void Init(object sender, EventArgs e)
        {
            base.Init(sender, e);
            manager.selectionChanged += createGizmos;
        }

        //! 
        //! Virtual function called before Unity destroys the TRACER _core.
        //! 
        //! @param sender A reference to the TRACER _core.
        //! @param e Arguments for these event. 
        //! 
        public override void Dispose()
        {
            base.Dispose();

            manager.selectionChanged -= createGizmos;
            diosposeGizmos();
        }
        bool HasNegativeScaleInHierarchy(Transform obj)
        {
            if (obj == null)
                return false;

            Vector3 scale = obj.localScale;
            if (scale.x < 0 || scale.y < 0 || scale.z < 0)
                return true; // Found a negative scale

            // Recursively check the parent
            return HasNegativeScaleInHierarchy(obj.parent);
        }

        //!
        //! Function that parses the given list of scene objects to create and
        //! add gizmo objects depending on it's type as child objects.
        //!
        private void createGizmos(object sender, List<SceneObject> sceneObjects)
        {
            diosposeGizmos();

            foreach (SceneObject sceneObject in sceneObjects)
            {
                VPETGizmo gizmo = null;
                if (HasNegativeScaleInHierarchy(sceneObject.transform))
                {
                    _negative = true;
                }
                switch (sceneObject)
                {
                    case SceneObjectLight:
                        {
                            gizmo = new VPETGizmo(sceneObject.name + "_Gizmo", sceneObject.transform);
                            Color lightColor = sceneObject.GetComponent<Light>().color;
                            Parameter<Color> colorParameter = sceneObject.getParameter<Color>("color");
                            colorParameter.hasChanged += gizmo.setColor;
                            m_eventHandlersColor.Add(new Tuple<Parameter<Color>, EventHandler<Color>>(colorParameter, gizmo.setColor));
                            switch (sceneObject)
                            {
                                case SceneObjectPointLight:
                                    {
                                        gizmo.addElement(ref m_circlePos, lightColor, true).localScale = new Vector3(2,2,2);
                                        Transform sphere = gizmo.addElement(ref m_circlePos, lightColor, true);
                                        sphere.localScale = new Vector3(2, 2, 2);
                                        sphere.localRotation = Quaternion.Euler(new Vector3(90, 0, 0));

                                        sceneObject._gizmo = gizmo.root.transform;
                                        updateScalePoint(sceneObject, null);
                                        sceneObject.hasChanged += updateScalePoint;
                                        m_ParameterEventHandlers.Add(new Tuple<SceneObject, EventHandler<AbstractParameter>>(sceneObject, updateScalePoint));
                                        break;
                                    }
                                case SceneObjectDirectionalLight:
                                    {
                                        switch (sceneObject)
                                        {
                                            case SceneObjectSunLight:
                                                calculateEclipticPos((SceneObjectSunLight)sceneObject);
                                                gizmo.addElement(ref m_eclipticPos, lightColor, true, false);
                                                sceneObject.hasChanged += updateEclipticPos;
                                                sceneObject._gizmo = gizmo.root.transform;
                                                break;
                                            default:
                                                gizmo.addElement(ref m_circlePos, lightColor, true);
                                                gizmo.addElement(ref m_linePos, lightColor);
                                                sceneObject._gizmo = gizmo.root.transform;
                                                break;
                                        }
                                        break;
                                    }
                                case SceneObjectSpotLight:
                                    {
                                        gizmo.addElement(ref m_conePos, lightColor).localScale = new Vector3(0.7071f, 0.7071f, 1f);
                                        gizmo.addElement(ref m_circlePos, lightColor, true).localPosition = new Vector3(0,0,1);

                                        sceneObject._gizmo = gizmo.root.transform;
                                        updateScaleSpot(sceneObject, null);
                                        sceneObject.hasChanged += updateScaleSpot;
                                        m_ParameterEventHandlers.Add(new Tuple<SceneObject, EventHandler<AbstractParameter>>(sceneObject, updateScaleSpot));
                                        break;
                                    }
                            }
                            break;
                        }
                     case SceneObjectCamera:
                        {
                            gizmo = new VPETGizmo(sceneObject.name + "_Gizmo", sceneObject.transform);
                            //GizmoElementUpdate nearPlane = gizmo.addElement(ref m_rectPos, Color.yellow, true);
                            gizmo.addElement(ref m_conePos, Color.yellow, false);
                            gizmo.addElement(ref m_rectPos, Color.yellow, true).localPosition = new Vector3(0,0,1);

                            sceneObject._gizmo = gizmo.root.transform;
                            updateScaleCamera(sceneObject, null);
                            sceneObject.hasChanged += updateScaleCamera;
                            m_ParameterEventHandlers.Add(new Tuple<SceneObject, EventHandler<AbstractParameter>>(sceneObject, updateScaleCamera));
                            break;
                        }
                    //[SEIM test for schematic view]
                    //case SceneObject:
                    //    {
                    //        gizmo = new VPETGizmo(sceneObject.name + "_Gizmo", sceneObject.transform);
                    //        gizmo.addElement(ref m_rectPos, Color.yellow, true).localPosition = new Vector3(0, 0, 0);

                    //        sceneObject._gizmo = gizmo.root;
                    //        //updateScaleCamera(sceneObject, null);
                    //        //sceneObject.hasChanged += updateScaleCamera;
                    //        //m_ParameterEventHandlers.Add(new Tuple<SceneObject, EventHandler<AbstractParameter>>(sceneObject, updateScaleCamera));
                    //        break;
                    //    }
                }
                if (gizmo != null)
                {
                    m_gizmos.Add(gizmo);
                }
            }
        }

        //!
        //! Function for calculating and setting of scale updates for a point light gizmo.
        //!
        private void updateScalePoint(object sender, AbstractParameter parameter)
        {
            SceneObjectPointLight sceneObject = (SceneObjectPointLight) sender;

            float range = sceneObject.range.value;
            if (_negative)
            {
                sceneObject._gizmo.localScale = new Vector3(range, range, -range);
            }
            else
            {
                sceneObject._gizmo.localScale = new Vector3(range, range, range);
            }
        }

        //!
        //! Function for calculating and setting of scale updates for a spot light gizmo.
        //!
        private void updateScaleSpot(object sender, AbstractParameter parameter)
        {
            SceneObjectSpotLight sceneObject = (SceneObjectSpotLight)sender;
            float range = sceneObject.range.value;
            float angle = sceneObject.spotAngle.value;

            // diameter = 2 * distance * tan( angle * 0.5 )
            float dia = 2f * range * MathF.Tan(angle / 180f * Mathf.PI * 0.5f);
            if (!_negative)
            {
                sceneObject._gizmo.localScale = new Vector3(dia, dia, range);
            }
            else
            {
                sceneObject._gizmo.localScale = new Vector3(dia, dia, -range);

            }
        }

        //!
        //! Function for calculating and setting of scale updates for a camera gizmo.
        //!
        private void updateScaleCamera(object sender, AbstractParameter parameter)
        {
            SceneObjectCamera sceneObject = (SceneObjectCamera)sender;
            float far = sceneObject.far.value;
            float fov = sceneObject.fov.value;
            float aspect = sceneObject.aspect.value;

            // diameter = 2 * distance * tan( angle * 0.5 )
            float dia = 2f * far * MathF.Tan(fov / 180f * Mathf.PI * 0.5f);

            if (!_negative)
            {
                sceneObject._gizmo.localScale = new Vector3(dia * aspect, dia, far);
            }
            else
            {
                sceneObject._gizmo.localScale = new Vector3(dia * aspect, dia, -far);
            }
        }

        //!
        //! Function updating the gizmo representing the suns ecliptic.
        //!
        private void calculateEclipticPos(SceneObjectSunLight sun)
        {
            try
            {
                float declination = -23.45f * Mathf.Deg2Rad * Mathf.Cos(0.9863f * Mathf.Deg2Rad * (sun.m_date.DayOfYear + 10));
                float floatMinute = 60f * (-0.171f * Mathf.Sin(0.0337f * sun.m_date.DayOfYear + 0.465f) - 0.1299f * Mathf.Sin(0.01787f * sun.m_date.DayOfYear - 0.168f));
                float sinLatitute = Mathf.Sin(sun.m_latitude.value * Mathf.Deg2Rad); // breite
                float sinDeclination = Mathf.Sin(declination);
                float cosLatitute = Mathf.Cos(sun.m_latitude.value * Mathf.Deg2Rad);
                float cosDeclination = Mathf.Cos(declination);

                for (int i = 0; i < m_eclipticPos.Length; i++)
                {
                    float hourAngle = 15f * (((float)i) / m_eclipticPos.Length * 24f - (15f - sun.m_longitude.value) / 15.0f - 12f + floatMinute / 60f);
                    float cosHourAngle = Mathf.Cos(hourAngle * Mathf.Deg2Rad);
                    float sinSunHeight = sinLatitute * sinDeclination + cosLatitute * cosDeclination * cosHourAngle;
                    float sunHeight = Mathf.Asin(sinSunHeight) * Mathf.Rad2Deg;
                    float sunDirection = Mathf.Acos(-(sinLatitute * sinSunHeight - sinDeclination) / (cosLatitute * Mathf.Sin(Mathf.Acos(sinSunHeight)))) * Mathf.Rad2Deg;

                    if (hourAngle > 0)
                        sunDirection = 360f - sunDirection;

                    Quaternion rotation = Quaternion.Euler(sunHeight, sunDirection, 0.0f);
                    m_eclipticPos[i] = rotation * -Vector3.forward;
                }
            }
            catch (Exception e)
            {
                Helpers.Log(e.ToString(), Helpers.logMsgType.WARNING);
            }
        }

        private void updateEclipticPos(object sender, AbstractParameter parameter)
        {
            SceneObjectSunLight sun = sender as SceneObjectSunLight;
            calculateEclipticPos(sun);
            VPETGizmo gizmo = m_gizmos.Find(g => g.root == sun._gizmo.gameObject);
            gizmo?.updateElement(0, ref m_eclipticPos);
        }

        //!
        //! Function for disposing and cleanup of all created gizmos.
        //!
        private void diosposeGizmos()
        {
            foreach (Tuple<Parameter<Color>, EventHandler<Color>> t in m_eventHandlersColor)
                t.Item1.hasChanged -= t.Item2;

            m_eventHandlersColor.Clear();
            
            foreach (Tuple<SceneObject, EventHandler<AbstractParameter>> t in m_ParameterEventHandlers)
                t.Item1.hasChanged -= t.Item2;
           
            m_ParameterEventHandlers.Clear();

            foreach (VPETGizmo gizmo in m_gizmos)
                gizmo.dispose();
           
            m_gizmos.Clear();
        }

    }
}
