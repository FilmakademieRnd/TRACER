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

//! @file "ColorSelect.cs"
//! @brief implementation of a color picker.
//! @author Paulo Scatena
//! @author Simon Spielmann
//! @version 0
//! @date 02.04.2022

using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

//TODO: Implement that we cannot move from color-square to color-line

namespace tracer
{
    public class ColorSelect : Manipulator, IBeginDragHandler, IEndDragHandler, IDragHandler, IPointerClickHandler
    {
        private Vector2 pickerSize;

        private Color outputColor;

        private Material mat;

        // development phase
        //private Image testImage;
        private float hue;
        
        private float sat;
        private float val;
        private float val2;

        private float speed = 1f;

        private bool hueDrag = false;

        private Parameter<Color> col = null;

        private Canvas _canvas;

        //!
        //! Init function of the ColorSelect that needs to be called manually 
        //! @param color This is the color parameter to be displayed and edited
        //!
        public override void Init(AbstractParameter param, UIManager m)
        {
            
            abstractParam = param;
            
            _canvas = GetComponentInParent<Canvas>();
            // Grab picker dimensions
            RectTransform rect = GetComponent<RectTransform>();
            pickerSize = rect.rect.size * _canvas.scaleFactor;

            // Grab material
            mat = GetComponent<Image>().material;

            col = (Parameter<Color>)param;
            col.hasChanged += updateColor;

            Color inColor = col.value;
            Debug.Log("<color=red>ColorSelect.Init inColor: "+inColor+"</color>");

            // Decompose into HSV components
            Color.RGBToHSV(inColor, out hue, out sat, out val);
            
            // Use pure hue for the material input
            Color shaderColor = Color.HSVToRGB(hue, 1f, 1f);

            mat.SetColor("_InputColor", shaderColor);

            // Set indicator coordinates
            mat.SetVector("_InputPos", new(sat * .8f, val, .9f, hue));

            // Output starts as the input
            outputColor = inColor;

        }

        public void updateColor(object sender, Color c)
        {
            //Debug.Log("<color=red>ColorSelect.updateColor</color>");
            Color.RGBToHSV(c, out hue, out sat, out val);

            // Use pure hue for the material input
            Color shaderColor = Color.HSVToRGB(hue, 1f, 1f);
            mat.SetColor("_InputColor", shaderColor);

            // Set indicator coordinates
            mat.SetVector("_InputPos", new(sat * .8f, val, .9f, hue));

            outputColor = c;
        }

        //!
        //! Unity function called by IPointerClickHandler on click
        //! @param data Data of the click event e.g. postion
        //!
        public void OnPointerClick(PointerEventData eventData)
        {
            //we need this, otherwise we can not click/tap to change the color
            OnBeginDrag(eventData);
            OnDrag(eventData);
        }


        //!
        //! Unity function called by IBeginDragHandler when a drag starts
        //! @param data Data of the drag event e.g. postion, delta, ...
        //!
        public void OnBeginDrag(PointerEventData data)
        {
            Vector3 clickPos = data.position;
            clickPos -= transform.position;
            clickPos /= pickerSize;
            // Identify area of operation - TODO: get rid of magic number?
            hueDrag = clickPos.x > .3f;
            //Debug.Log("Begin: " + clickPos.ToString());
        }

        //!
        //! Unity function called by IDragHandler when a drag is currently performed
        //! @param data Data of the drag event e.g. postion, delta, ...
        //!
        public void OnDrag(PointerEventData data)
        {
            Vector3 clickPos = data.position;
            clickPos -= transform.position;
            clickPos /= pickerSize;
            clickPos.y = Mathf.Clamp(clickPos.y + .5f, 0f, 1f);
            //Debug.Log("Drag: " + clickPos.ToString());
            if (hueDrag)
            {
                hue = clickPos.y;
                mat.SetColor("_InputColor", Color.HSVToRGB(hue, 1f, 1f));
            }
            else
            {
                val = clickPos.y;
                sat = Mathf.Clamp((clickPos.x + .5f) * 1.25f, 0f, 1f);
            }
            outputColor = Color.HSVToRGB(hue, sat, val);

            mat.SetVector("_InputPos", new(sat * .8f, val, .9f, hue));

            if (col != null)
                col.setValue(outputColor);
        }

        //!
        //! Unity function called by IEndDragHandler when a drag ends
        //! @param data Data of the drag event e.g. postion, delta, ...
        //!
        public void OnEndDrag(PointerEventData data)
        {
            InvokeDoneEditing(this, true);
        }

        public void controllerManipulator(Vector3 input)
        {
            
            hue += input.z * speed * Time.deltaTime;
            if (hue > 1)
            {
                hue = 1;
            }
            else if (hue < 0)
            {
                hue = 0;
            }
            mat.SetColor("_InputColor", Color.HSVToRGB(hue, 1f, 1f));
            
            val += input.y* speed * Time.deltaTime;
            val2 += input.x* speed * Time.deltaTime;
            if (val > 1)
            {
                val = 1;
            }
            else if (val < 0)
            {
                val = 0;
            }
            if (val2 > 1)
            {
                val2 = 1;
            }
            else if (val2 < 0)
            {
                val2 = 0;
            }
            
            sat = Mathf.Clamp((val2 ) * 1.25f, 0f, 1f);
            
            
            outputColor = Color.HSVToRGB(hue, sat, val);
            
            mat.SetVector("_InputPos", new(sat * .8f, val, .9f, hue));

            if (col != null)
                col.setValue(outputColor);
        }

    }
}


