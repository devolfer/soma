using UnityEngine;

namespace Devolfer.Soma
{
    public class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionFieldName { get; }

        public ShowIfAttribute(string conditionFieldName) => ConditionFieldName = conditionFieldName;
    }
}