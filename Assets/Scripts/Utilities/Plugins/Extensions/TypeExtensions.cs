using System;

namespace Neo.Utility.Extensions {
    public static class TypeExtensions {
        public static Type	TypeOf<T>( T t ) {
    		return t != null ? t.GetType() : typeof(T);
    	}
    }
}