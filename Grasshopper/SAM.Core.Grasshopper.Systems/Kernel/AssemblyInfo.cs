﻿using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace SAM.Core.Grasshopper.Systems
{
    public class AssemblyInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "SAM";
            }
        }

        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return Properties.Resources.HL_Logo24; ;
            }
        }

        public override Bitmap AssemblyIcon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return Properties.Resources.HL_Logo24; ;
            }
        }

        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "SAM Grashopper Toolkit, please explore";
            }
        }

        public override Guid Id
        {
            get
            {
                return new Guid("f7034e04-c20f-4cfe-8ed5-b0b29944kacd");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "Michal Dengusiak & Jakub Ziolkowski at Hoare Lea";
            }
        }

        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "Michal Dengusiak -> michaldengusiak@hoarelea.com and Jakub Ziolkowski -> jakubziolkowski@hoarelea.com";
            }
        }
    }
}