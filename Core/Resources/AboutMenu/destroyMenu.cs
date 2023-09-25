/*
TRACER FOUNDATION - 
Toolset for Realtime Animation, Collaboration & Extended Reality
tracer.research.animationsinstitut.de
https://github.com/FilmakademieRnd/TRACER

Copyright (c) 2023 Filmakademie Baden-Wuerttemberg, Animationsinstitut R&D Lab

TRACER is a development by Filmakademie Baden-Wuerttemberg, Animationsinstitut
R&D Labs in the scope of the EU funded project MAX-R (101070072) and funding on
the own behalf of Filmakademie Baden-Wuerttemberg.  Former EU projects Dreamspace
(610005) and SAUCE (780470) have inspired the TRACER development.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the MIT License for more details.
You should have received a copy of the MIT License along with this program; 
if not go to https://opensource.org/licenses/MIT
*/

using UnityEngine;
using UnityEngine.UI;

public class destroyMenu : MonoBehaviour
{
    private Button m_button;
    // Start is called before the first frame update
    void Start()
    {
        Transform button = transform.GetChild(0).Find("PanelMenu").Find("Button");
        m_button = button.GetComponent<Button>();
        m_button.onClick.AddListener(DestroyThis);
    }

    private void DestroyThis()
    {
        Destroy(transform.gameObject);
    }
}
