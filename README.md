# TypeCaster

This is a solution to cast data from one type to another without the need to know which is which beforehand.
This started as a tool to cast a generic enum to another enum or to/from integer without need to box/unbox data. 
Now it can be used to cast any type tp another type if a casting method or operator is defined on either type.
Whenever the user Invokes "TypeCaster<T, TResult>.Cast" method, TypeCaster will attempt to use the best available casting method that's defined on T, TResult or global caster database.
