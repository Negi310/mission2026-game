using UnityEngine;
using UnityEngine.UI;
namespace TankTown
{
    internal sealed class TankTownCardArt : Graphic
    {
        internal string Symbol;
        private VertexHelper vertices;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); vertices = vh;
            if (Symbol == "CITY" || Symbol == "BIG")
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = -105 + i * 46, height = i % 2 == 0 ? 60 : 96;
                    Box(x, -46, 37, height, color);
                    for (int y = 0; y < (int)height / 23; y++) { Box(x + 7, -38 + y * 22, 7, 9, Color.white); Box(x + 22, -38 + y * 22, 7, 9, Color.white); }
                }
            }
            else if (Symbol == "PARK")
            {
                Ring(Vector2.zero, 47, 5, color);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4; var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 47;
                    Line(Vector2.zero, p, 2, color); Box(p.x - 9, p.y - 13, 18, 14, color);
                }
                Line(new Vector2(-30,-54), Vector2.zero, 7, color); Line(new Vector2(30,-54), Vector2.zero, 7, color);
            }
            else if (Symbol == "SWEET")
            {
                Box(-83,-48,166,48,color);Box(-60,0,120,30,Color.white);Box(-55,30,110,15,color);
                for(int i=0;i<3;i++){Ring(new Vector2(-34+i*34,57),10,10,new Color(1,.24f,.4f));}
            }
            else if (Symbol == "HOME")
            {
                Line(new Vector2(-55,0),new Vector2(0,45),9,color);Line(new Vector2(0,45),new Vector2(55,0),9,color);
                Box(-38,-46,76,46,color);Box(-9,-46,18,31,Color.white);
            }
            else if (Symbol == "BEST" || Symbol == "RANK")
            {
                Box(-32,-5,64,47,color);Box(-5,-35,10,30,color);Box(-30,-45,60,10,color);
                Ring(new Vector2(-33,22),20,6,color);Ring(new Vector2(33,22),20,6,color);
            }
            else
            {
                Ring(Vector2.zero, 43, 9, color);
                if(Symbol == "GO!" || Symbol == "GO") {Line(new Vector2(-9,-20),new Vector2(16,0),7,color);Line(new Vector2(16,0),new Vector2(-9,20),7,color);}
                else {Box(-5,-24,10,35,color);Box(-5,20,10,10,color);}
            }
        }
        private void Box(float x,float y,float w,float h,Color tint)
        { Quad(new Vector2(x,y),new Vector2(x,y+h),new Vector2(x+w,y+h),new Vector2(x+w,y),tint); }
        private void Line(Vector2 a,Vector2 b,float width,Color tint)
        {var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(a-n,a+n,b+n,b-n,tint);}
        private void Ring(Vector2 center,float radius,float width,Color tint)
        {for(int i=0;i<40;i++){float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));Quad(center+p*(radius-width),center+p*radius,center+q*radius,center+q*(radius-width),tint);}}
        private void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
        {int start=vertices.currentVertCount;vertices.AddVert(a,tint,Vector2.zero);vertices.AddVert(b,tint,Vector2.zero);vertices.AddVert(c,tint,Vector2.zero);vertices.AddVert(d,tint,Vector2.zero);vertices.AddTriangle(start,start+1,start+2);vertices.AddTriangle(start,start+2,start+3);}
    }
}
