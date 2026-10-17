import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.symbol.*;
import java.util.*;

public class Callers2 extends GhidraScript {
    void doTarget(String label, String addrStr, FunctionManager fm, ReferenceManager rm,
                  DecompInterface di, Set<String> dumped) throws Exception {
        Address a = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(addrStr);
        Function tf = fm.getFunctionAt(a);
        println("");
        println("################ TARGET " + label + " @ " + addrStr
                + "  fn=" + (tf==null?"(none)":tf.getName())
                + " size=" + (tf==null?"?":(""+tf.getBody().getNumAddresses())));
        ReferenceIterator it = rm.getReferencesTo(a);
        int n=0;
        List<Function> callers = new ArrayList<Function>();
        while (it.hasNext() && n < 60) {
            Reference r = it.next();
            Function f = fm.getFunctionContaining(r.getFromAddress());
            println("   <- " + r.getFromAddress() + "  [" + r.getReferenceType() + "]  in "
                    + (f==null? "(data/none)" : (f.getName()+" @ "+f.getEntryPoint())));
            n++;
            if (f != null && !callers.contains(f)) callers.add(f);
        }
        if (n==0) println("   (无引用)");
        for (Function f : callers) {
            if (!dumped.add(f.getName())) continue;
            println("");
            println("============ DECOMPILE " + f.getName() + " @ " + f.getEntryPoint()
                    + " size=" + f.getBody().getNumAddresses() + " ============");
            try {
                DecompileResults dr = di.decompileFunction(f, 90, monitor);
                if (dr != null && dr.decompileCompleted() && dr.getDecompiledFunction() != null)
                    println(dr.getDecompiledFunction().getC());
                else println("(decompile failed)");
            } catch (Throwable t) { println("(exception " + t + ")"); }
        }
    }
    @Override
    public void run() throws Exception {
        FunctionManager fm = currentProgram.getFunctionManager();
        ReferenceManager rm = currentProgram.getReferenceManager();
        DecompInterface di = new DecompInterface();
        di.toggleCCode(true); di.toggleSyntaxTree(true);
        if (!di.openProgram(currentProgram)) { println("!! open failed"); return; }
        Set<String> dumped = new LinkedHashSet<String>();
        doTarget("FUN_00149394 (触摸可用性判定)", "00149394", fm, rm, di, dumped);
        doTarget("Check_PAS_MenuSelectMode (本地定义)", "00340628", fm, rm, di, dumped);
        println("@@@@ DONE");
    }
}