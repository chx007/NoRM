using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Norm.BSON;

namespace Norm.Commands.Qualifiers
{
    //HACK: 增加实体关联的$ref属性查询
    public class RefQualifier : QualifierCommand
    {
        internal RefQualifier(string value)
            : base("$ref", value)
        {
        }
    }
}
