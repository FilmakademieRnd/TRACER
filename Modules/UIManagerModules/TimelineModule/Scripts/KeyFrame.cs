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

namespace tracer{
	public class KeyFrame : MonoBehaviour{
	    public AbstractKey key;
		private RectTransform m_rectTransform;
		private Image m_image;
	
	    private Vector3 m_lastPosition = Vector3.zero;
	    
		private int index;	//the index in our AnimationParameter keyList
		private bool isSelected = false;
		/*
		private UnityAction<AbstractKey, float> m_keyframeDragEndEvent;
	    private UnityAction<GameObject> m_keyframeSelectedEvent;

	    public UnityAction<AbstractKey, float> KeyframeDragEndEvent
	    {
	        set { m_keyframeDragEndEvent = value; }
	    }

        public UnityAction<GameObject> KeyframeSelectedEvent
        {
            set { m_keyframeSelectedEvent = value; }
        }*/

        void Awake(){
	        m_rectTransform = transform.GetComponent<RectTransform>();
			m_image = transform.GetComponent<Image>();
			m_lastPosition = m_rectTransform.position;
        }

		public void UpdateIndex(int _index){ index = _index; }
		public int GetIndex(){ return index; }

		public bool WasHit(GameObject evaluatedHitGO){ return evaluatedHitGO == m_image.gameObject; }

		public void Select(){		m_image.color = Color.blue; isSelected = true;}
        public void DeSelect(){		m_image.color = new Color(1.0f, 0.517f, 0,216); isSelected = false;}
		public void DebugColor(Color c){		m_image.color = c; }

		public Vector3 GetPos(){	return m_rectTransform.position; }
		public void SetLocalPos(float localX){ m_rectTransform.localPosition = new Vector3(localX,0,0); }

        public void DragStart(){	
			m_lastPosition = m_rectTransform.position;
			if (!isSelected) {
				m_image.color = Color.yellow;
			}
		}
	    public void Dragging(float evaluatedPosOnTimeline){
			m_rectTransform.localPosition = new Vector3(evaluatedPosOnTimeline, 0, 0);
        }
		public void AbortDragging() {
			//if we ended dragging on another kf, reset to original pos!
			m_rectTransform.position = m_lastPosition;

			if(!isSelected)
				DeSelect();	//reset color
		}
		public void DragEnd() {
			if(!isSelected)
				DeSelect();	//reset color
		}
	

		/*public override void OnPointerDown(PointerEventData data){
			base.OnPointerDown(data);
            m_keyframeSelectedEvent?.Invoke(transform.gameObject);
        }*/
    }
}