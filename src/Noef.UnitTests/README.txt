The unit tests have no binary dependency on Noef.  It uses the single cs file distribution, since that's the way Noef is intended to be used.
Because of this, there is a pre-build step for Noef.UnitTests that creates a fresh copy of _Noef.cs from the current Noef source.
