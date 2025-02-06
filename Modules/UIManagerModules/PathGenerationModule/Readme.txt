//! @folder "PathGenerationModuke"
//! @brief Implementation of the TRACER PathGenerationModule
//! @author Thomas "Kruegbert" Krüger
//! @version 1
//! @date 06.02.2025

GOAL
- have UI options to generate a path for characters within unity
- store these path data somewhere on the character
- send these variables to AnimHost and wait for the result
- the result should be an runtime generated walking animation for the character for the path we generated

MVP
- Selecting any char will add a button to our MainMenu
- this Button opens the PathFindingModule tools
- we can click the "linear path button", then anywhere in the world: a linear path will be generated
- another button appears which will trigger the AnimHost generation progress

THOUGHTS
- the data we get should be stored somewhere, so we can play it again manually (or even modify it)?
- it should not be stored on TRS anim data, since this would overwrite possible previous data
- is it possible to store more than one path on the object via a list of parameter data? (always append?)
-- if so, we could visualize it via this module (not in the Timeline) like the Measurement tools
-- we show all path data points with pos/rot AND "key-time"