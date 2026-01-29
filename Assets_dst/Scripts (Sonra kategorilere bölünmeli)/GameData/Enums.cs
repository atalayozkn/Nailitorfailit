namespace Interactions
{
    public enum MaterialType
    {
        None,
        Brick,
        Wood,
        MetalRod,
        MetalPlate,
        Cement
    }

    public enum ConstructType
    {
        Frame, //Frame wall olacak, wall boyanacak fln idk
        Wall,
        Floor,
        BrokenWall, //Gelecekte yıkım olursa
        BrokenFloor

    }

    public enum Tools
    {
        //İnşaatla ilgili bir oyun olduğu için alet alma, aletin nerde olduğunu arama gibi şeyler olmazsa olmaz
        PaintRoller, //Olursa duvar badana
        Hammer, //Olura yıkım
        Bag, //Yıkımdan sonra yerde moloz falan olur, uğraştırıcı ama önemli bir nokta bence
             //çok doldurursan ağır oluyor çok yavaşlıyorsun fln, keyifli olmazsa sadece broom da olabilir
        Broom, //Etrafı temizlemen lazım tozlanır sonuçta
        Wrench, //Tadilat olursa, senin aletlerin bozulursa diye
        Drill, //Nasıl kulalnılır bilmem ama olabilir
        Ladder //Janky bir merdiven olmazsa olmaz


    }
}