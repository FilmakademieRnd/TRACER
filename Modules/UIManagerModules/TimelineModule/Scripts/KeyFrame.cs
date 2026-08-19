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
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace tracer{
	public class KeyFrame : MonoBehaviour{
	    public AbstractKey key;
		private RectTransform m_rectTransform;
		private Image m_image;
	
	    private Vector3 m_lastPosition = Vector3.zero;
	    private bool isSelected = false;
		private int index;	//the index in our AnimationParameter keyList
		private int siblingIndexWas = 0;
		
		private enum KeyFrameState {
			wasCreated = 0,
			idle = 5,
			selected = 10,
			isDragged = 20,
			isDraggedError = 30,	//dragged onto other keyframe time
			notSelectable = 40		//in play mode
		}

		private KeyFrameState state = KeyFrameState.wasCreated;
		private Coroutine colorCoroutine;
		private Color standardColor = new Color(1.0f, 0.517f, 0, 1);
		private Color selectedColor = Color.blue;
		private Color draggedColor = Color.yellow;
		private Color draggedErrorColor = Color.red;
		private Color notSelectableColor = new Color(1.0f, 0.517f, 0, 0.2f);
		private Color highlightColor = Color.white;

        void Awake(){
	        m_rectTransform = transform.GetComponent<RectTransform>();
			m_image = transform.GetComponent<Image>();
			m_lastPosition = m_rectTransform.position;

			state = KeyFrameState.wasCreated;

			AdjustColor();
        }

		public void UpdateIndex(int _index){ index = _index; }
		public int GetIndex(){ return index; }

		public bool WasHit(GameObject evaluatedHitGO){ return evaluatedHitGO == m_image.gameObject; }
		public bool IsSelected(){ return isSelected; }

		public void Select(){			isSelected = true; state = KeyFrameState.selected; AdjustColor(); }
        public void DeSelect(){			isSelected = false; state = KeyFrameState.idle; AdjustColor(); }
		
		public void SetPlayMode(bool inPlayMode){
			if (inPlayMode) {
				isSelected = false;
				state = KeyFrameState.notSelectable;
			} else {
				state = KeyFrameState.idle;
			}
			AdjustColor();
		}
		//simple visual fluff
		public void HighlightIfTimePassedThisKeyFrame(float prevTime, float currentTime) {
			if(prevTime <= key.time && currentTime > key.time){
				m_image.color = highlightColor;
				AdjustColor();
			}
		}

		public Vector3 GetPos(){	return m_rectTransform.position; }
		public void SetLocalPos(float localX){ m_rectTransform.localPosition = new Vector3(localX,0,0); }

        public void DragStart(){	
			m_lastPosition = m_rectTransform.position;

			siblingIndexWas = m_rectTransform.GetSiblingIndex();
			m_rectTransform.SetSiblingIndex(0);
			
			state = KeyFrameState.isDragged;
			AdjustColor();
		}
	    public void Dragging(float evaluatedPosOnTimeline){
			m_rectTransform.localPosition = new Vector3(evaluatedPosOnTimeline, 0, 0);
        }
		public void DragError(bool inErrorState) {
			if(inErrorState && state == KeyFrameState.isDragged) {
				state = KeyFrameState.isDraggedError; 
				AdjustColor();
			}else if(!inErrorState && state == KeyFrameState.isDraggedError) {
				state = KeyFrameState.isDragged; 
				AdjustColor();
			}
		}
		public void AbortDragging() {
			//if we ended dragging on another kf, reset to original pos!
			m_rectTransform.position = m_lastPosition;
			RevertSetting();
		}

		public void DragEnd() {
			RevertSetting();
		}

		private void RevertSetting() {
			m_rectTransform.SetSiblingIndex(siblingIndexWas);
			if (isSelected) {
				state = KeyFrameState.selected;
				AdjustColor();
			} else {
				state = KeyFrameState.idle;
				AdjustColor();
			}
		}

		private void AdjustColor() {
			if(colorCoroutine != null)
				StopCoroutine(colorCoroutine);

			switch (state) {
				case KeyFrameState.wasCreated:
					Color invisibleStartColor = standardColor;
					invisibleStartColor.a = 0f;
					m_image.color = invisibleStartColor;
					if(gameObject.activeInHierarchy)
						colorCoroutine = StartCoroutine(ColorCoroutine(standardColor, 1f));
					else m_image.color = standardColor;
					break;
				case KeyFrameState.idle:
					if(gameObject.activeInHierarchy)
						colorCoroutine = StartCoroutine(ColorCoroutine(standardColor, 1f));
					else m_image.color = standardColor;
					break;
				case KeyFrameState.selected:
					if(gameObject.activeInHierarchy)
						colorCoroutine = StartCoroutine(ColorCoroutine(selectedColor, 0.5f));
					else m_image.color = selectedColor;
					break;
				case KeyFrameState.isDragged:
					if(gameObject.activeInHierarchy)
						colorCoroutine = StartCoroutine(ColorCoroutine(draggedColor, 0.5f));
					else m_image.color = draggedColor;
					break;
				case KeyFrameState.isDraggedError:
					if(gameObject.activeInHierarchy)
						colorCoroutine = StartCoroutine(ColorCoroutine(draggedErrorColor, 0.5f));
					else m_image.color = draggedErrorColor;
					break;
				case KeyFrameState.notSelectable:
					if(gameObject.activeInHierarchy)
						colorCoroutine = StartCoroutine(ColorCoroutine(notSelectableColor, 0.3f));
					else m_image.color = notSelectableColor;
					break;
			}
		}
	

		private IEnumerator ColorCoroutine(Color endColor, float duration) {
			float t = 0f;
			duration = Mathf.Clamp(duration, 0.01f, 10f);
			Color startColor = m_image.color;
			while (t < 1f) {
				t += Time.deltaTime/duration;
				m_image.color = Color.Lerp(startColor, endColor, t);
				yield return null;
			}
			m_image.color = endColor;
		}
    }
}