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
	public class KeyFrame : Button, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		private RectTransform timelineTransform;

	    private RectTransform rectTransform;
	
	    private Vector3 lastPosition = Vector3.zero;
	
	    public AbstractKey key;
	
	    private UnityAction<AbstractKey, float> callback;
	
	    public UnityAction<AbstractKey, float> Callback
	    {
	        set { callback = value; }
	    }

        protected void Awake()
	    {
	        base.Awake();
	        // get rectTransform component
	        rectTransform = transform.GetComponent<RectTransform>();
			timelineTransform = transform.parent.GetComponent<RectTransform>();
	    }
	
	    // DRAG
	    public void OnBeginDrag(PointerEventData data)
	    {
	        lastPosition = rectTransform.position;
	    }
	
	    public void OnDrag(PointerEventData data)
	    {
			float newX = lastPosition.x + data.position.x - data.pressPosition.x;
			if (newX - rectTransform.rect.width * 0.5f > timelineTransform.position.x - timelineTransform.rect.width * 0.5f &&
				newX + rectTransform.rect.width * 0.5f < timelineTransform.position.x + timelineTransform.rect.width * 0.5f) 
				rectTransform.position = new Vector3(lastPosition.x + data.position.x - data.pressPosition.x, lastPosition.y, lastPosition.z);
		}
	
	    public void OnEndDrag(PointerEventData data)
	    {
	        callback?.Invoke(key, rectTransform.position.x);
	    }
    }
}