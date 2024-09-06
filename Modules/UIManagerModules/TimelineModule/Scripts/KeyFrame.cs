/*
-----------------------------------------------------------------------------
This source file is part of VPET - Virtual Production Editing Tool
http://vpet.research.animationsinstitut.de/
http://github.com/FilmakademieRnd/VPET

Copyright (c) 2018 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

This project has been initiated in the scope of the EU funded project 
Dreamspace under grant agreement no 610005 in the years 2014, 2015 and 2016.
http://dreamspaceproject.eu/
Post Dreamspace the project has been further developed on behalf of the 
research and development activities of Animationsinstitut.

This program is free software; you can redistribute it and/or modify it under
the terms of the MIT License as published by the Open Source Initiative.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.

You should have received a copy of the MIT License along with
this program; if not go to
https://opensource.org/licenses/MIT
-----------------------------------------------------------------------------
*/
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace tracer
{
	public class KeyFrame : Button, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
	{
	    public AbstractKey key;
		
		private RectTransform m_timelineTransform;

	    private RectTransform m_rectTransform;
	    
		private Image m_image;
	
	    private Vector3 m_lastPosition = Vector3.zero;
	
	    private UnityAction<AbstractKey, float> m_callback;
	    private UnityAction<KeyFrame> m_callback1;

		private float m_leftLimit, m_rightLimit = 0.0f;
	
	    public UnityAction<AbstractKey, float> Callback
	    {
	        set { m_callback = value; }
	    }

        public UnityAction<KeyFrame> Callback1
        {
            set { m_callback1 = value; }
        }

        protected void Awake()
	    {
	        base.Awake();
	        // get m_rectTransform component
	        m_rectTransform = transform.GetComponent<RectTransform>();
			m_image = transform.GetComponent<Image>();
            Transform tp = transform.parent;
			if (tp)
			{
				m_timelineTransform = tp.GetComponent<RectTransform>();
                m_leftLimit = m_timelineTransform.position.x - m_timelineTransform.rect.width * transform.parent.parent.localScale.x * 0.5f;
                m_rightLimit = m_timelineTransform.position.x + m_timelineTransform.rect.width * transform.parent.parent.localScale.x * 0.5f;
            }
        }

		public void select()
		{
			m_image.color = Color.blue;
		}

        public void deSelect()
        {
            m_image.color = new Color(1.0f, 0.517f, 0,216);
        }

        // DRAG
        public void OnBeginDrag(PointerEventData data)
	    {
	        m_lastPosition = m_rectTransform.position;
	    }
	
	    public void OnDrag(PointerEventData data)
	    {
			float newX = m_lastPosition.x + data.position.x - data.pressPosition.x;

            if (newX > m_leftLimit && newX < m_rightLimit)
                m_rectTransform.position = new Vector3(newX, m_lastPosition.y, m_lastPosition.z);
        }
	
	    public void OnEndDrag(PointerEventData data)
	    {
	        m_callback?.Invoke(key, m_rectTransform.position.x);
	    }

		public void OnPointerDown(PointerEventData data)
		{
            m_callback1?.Invoke(this);
        }
    }
}