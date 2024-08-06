# TRACER FOUNDATION - Toolset for Realtime Animation, Collaboration & Extended Reality

TRACER is a software agnostic communication infrastructure and toolset for plugging open-source tools into a production pipeline, establishing interoperability between open source and proprietary tools, targeting real-time collaboration and XR productions, with an operational layer for exchanging data objects and updates including animation and scene data, synchronization of scene updates of different client applications (Blender, UE, Unity, VPET ...), parameter harmonization between different engines/renderers, unified scene distribution and scene export which stores the current state of the scene. Furthermore, it can act as an Animation Host, to support XR and Virtual Production high demand to be able to use, exchange and direct animations in real-time environments. TRACER can be integrated and interact with any GameEngine (e.g. Unreal) or DCC (e.g. Blender, Maya, ...) through the provided open APIs and protocols.
![TRACER](.doc/img/tracer_info_graphics_shematic.png)
**TRACER web site:** https://research.animationsinstitut.de/tracer

## Repository Content

TRACER FOUNDATION features:

- An open transfer protocol to provide 3D scenes coming from any DCC, game engine or XR application
- Time-synchronized scene updates for real-time collaboration and multi-user applications
- Generalized parameter representation and synchronization
- Network communication through netMQ/zeroMQ
- AR support (incl. Tracking and Markers)
- Touch UI (Buttons and Navigation Gestures)
- Modular, extendible, module-based architecture
And much more...

The TRACER Foundation itself is developed in C#, thereby it is well suited to target any Desktop, VR or XR platform.

## TRACER Implementations

 - [VPET - Virtual Production Editing Tools](https://github.com/FilmakademieRnd/VPET)
 - [DataHub](https://github.com/FilmakademieRnd/DataHub)
 - [Location Based Expereince Example](https://github.com/FilmakademieRnd/LBXExample)

## About

![Animationsinstitut R&D](.doc/img/logo_rnd.jpg)

TRACER is a development by Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Labs in the scope of the EU funded project [MAX-R](https://max-r.eu/) (101070072) and funding on the own behalf of Filmakademie Baden-Wuerttemberg.  Former EU projects Dreamspace (610005) and SAUCE (780470) have inspired the TRACER development.

## Funding

![Animationsinstitut R&D](.doc/img/EN_FundedbytheEU_RGB_POS_rs.png)

This project has received funding from the European Union's Horizon Europe Research and Innovation Programme under Grant Agreement No 101070072 MAX-R.
This development was inspired by projects under the the European Union’s Horizon 2020 Research and Innovation Programme under Grant Agreement No 780470 SAUCE and the European Commission’s Seventh Framework Programme under grant agreement no 610005 DREAMSPACE.


## License
TRACER is a open-sorce development by Filmakademie Baden-Wuerttemberg's Animationsinstitut.  
The framework is licensed under [MIT](LICENSE.txt). See [License info file](LICENSE_Info.txt) for more details.
