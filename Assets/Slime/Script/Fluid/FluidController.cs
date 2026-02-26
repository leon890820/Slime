using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidController : MonoBehaviour{
    public FluidMaster fluidMaster;
    public FluidDisplay[] fluidDisplay;

    public DisPlayMode displayMode;

    public FluidDisplay FluidDisplay {
        get {
            return displayMode switch {
                DisPlayMode.PARTICEL => fluidDisplay[0],
                DisPlayMode.MARCHINGCUBE => fluidDisplay[1],
                DisPlayMode.DEBUG => fluidDisplay[2],
                _ => null,
            };
        }
    }

    private void Start() {
        fluidMaster.Initialized();
        foreach (FluidDisplay display in fluidDisplay) display.Init();
    }

    private void Update() { 
        fluidMaster.UpdateParticleSimulated();
        FluidDisplay.Display();
    }

}



public enum DisPlayMode {
    PARTICEL, MARCHINGCUBE, DEBUG
}
