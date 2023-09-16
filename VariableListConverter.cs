using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Microsoft.SqlServer.Dts.Runtime;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
    public class VariableListConverter : StringConverter
    {
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            if (context == null)
            {
                return new StandardValuesCollection(new string[]{"null context"});
            }
            if (context.Instance == null)
            {
                return new StandardValuesCollection(new string[]{"null context instance"});
            }
            var contextInstanceType = context.Instance.GetType();
            var taskHostProperty = contextInstanceType.GetProperty("PipelineTask", typeof (TaskHost));
            if (taskHostProperty == null)
            {
                return new StandardValuesCollection(new string[]{"null task host property"});
            }

            var taskHost = taskHostProperty.GetValue(context.Instance, null) as TaskHost;
            var variablesList = new List<string> {string.Empty};
            foreach(var v in taskHost.Variables)
            {
                if(!v.SystemVariable)
                {
                    variablesList.Add(v.QualifiedName);
                }
            }
            variablesList.Sort();
            return new StandardValuesCollection(variablesList);
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }
    }
}
