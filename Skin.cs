using System;
using System.Collections.Generic;

namespace FloatFromSkin
{
    class Skin
    {
        public ulong param_s;
        public ulong param_m;

        public ulong param_a;
        public ulong param_d;

        public float Float_Value;

        //public bool At_Least_One_Request_Has_Been_Made = false;
        public DateTime Last_Float_Request_Time;
        public bool Has_Float_Value = false;

        public List<Guid> Connection_Guids = new List<Guid>();

        public override string ToString()
        {
            string String_Representation = "";
            if (param_s != 0)
            {
                String_Representation += "S" + param_s.ToString();
            }
            else
            {
                String_Representation += "M" + param_m.ToString();
            }
            String_Representation += "A" + param_a.ToString();
            String_Representation += "D" + param_d.ToString();
            return String_Representation;
        }
    }
}
