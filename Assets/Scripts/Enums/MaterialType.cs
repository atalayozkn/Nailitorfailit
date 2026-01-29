namespace Interactions
{
    public enum MaterialType
    {
        None,
        Brick,
        Wood,
        HalfWood,
        MetalPlate,
        Cement
    }

    public static class MaterialExtensions
    {
 
        public static float GetWeight(this MaterialType type)
        {
            return type switch
            {
                MaterialType.Brick => 1.0f,
                MaterialType.Wood => 2.0f, // bu deðiþtirilip bölününce fln azalacak þekilde yapýlabilir
                MaterialType.HalfWood => 1.0f,
                MaterialType.MetalPlate => 8.0f,
                MaterialType.Cement => 5.0f,
                _ => 0.0f // Default for 'None' or undefined
            };
        }
        // bu .GetWeight() diyerek direkt çaðýrýlabilir 
    }
}