using UnityEngine;
using ItemScript;

namespace Interactions
{
    public interface IConstructable
    {
        ConstructType ConstructType { get; }
        bool IsBuilt { get; }
    }
}