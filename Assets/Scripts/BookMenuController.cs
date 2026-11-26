using System.Collections.Generic;
using UnityEngine;

public class BookMenuController : MonoBehaviour
{
    [Tooltip("Arrastra aquí TODOS los paneles que pueden aparecer a la derecha")]
    [SerializeField] private List<GameObject> paginasDerecha;

    public void MostrarPagina(GameObject paginaAEncender)
    {
        // Apaga todas las páginas registradas excepto la que acabas de pedir
        foreach (GameObject pagina in paginasDerecha)
        {
            if (pagina != null)
            {
                pagina.SetActive(pagina == paginaAEncender);
            }
        }
    }

    public void CerrarTodasLasPaginas()
    {
        foreach (GameObject pagina in paginasDerecha)
        {
            if (pagina != null)
            {
                pagina.SetActive(false);
            }
        }
    }
}