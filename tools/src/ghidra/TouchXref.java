import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.symbol.*;
import java.util.*;

public class TouchXref extends GhidraScript {
    @Override
    public void run() throws Exception {
        SymbolTable st = currentProgram.getSymbolTable();
        FunctionManager fm = currentProgram.getFunctionManager();
        ReferenceManager rm = currentProgram.getReferenceManager();
        Listing lst = currentProgram.getListing();

        String[] keys = {"isTouchToggleSupported","isTouchPanelPadSupported","setTouchable",
                         "MenuSelectMode","BarCtrlDial","GestureAnalyzer","calcLocalFlickSpeed",
                         "onMouseEvent","setMouseCapture","acceptMultiTouch"};

        println("@@@@ PROGRAM " + currentProgram.getName()
                + "  lang=" + currentProgram.getLanguageID()
                + "  base=" + currentProgram.getImageBase());
        int total=0; for (Function f : fm.getFunctions(true)) total++;
        println("@@@@ FUNCTIONS " + total);

        // 1) 所有匹配符号
        println("@@@@ MATCHING SYMBOLS @@@@");
        List<Symbol> matched = new ArrayList<Symbol>();
        SymbolIterator it = st.getAllSymbols(true);
        while (it.hasNext()) {
            Symbol s = it.next();
            String n = s.getName();
            for (String k : keys) {
                if (n.indexOf(k) >= 0) { matched.add(s); break; }
            }
        }
        for (Symbol s : matched) {
            println("SYM  " + s.getAddress() + "  ext=" + s.isExternal()
                    + "  " + s.getSymbolType() + "  " + s.getName());
        }

        // 2) 对每个匹配符号，找引用者并反编译所在函数
        println("@@@@ CALLERS + DECOMPILE @@@@");
        DecompInterface di = new DecompInterface();
        di.toggleCCode(true); di.toggleSyntaxTree(true);
        if (!di.openProgram(currentProgram)) { println("!! decompiler open failed"); return; }

        Set<String> doneFn = new LinkedHashSet<String>();
        for (Symbol s : matched) {
            Address a = s.getAddress();
            ReferenceIterator rit = rm.getReferencesTo(a);
            int c = 0;
            while (rit.hasNext() && c < 40) {
                Reference r = rit.next();
                Function f = fm.getFunctionContaining(r.getFromAddress());
                String key = (f == null) ? ("none@" + r.getFromAddress()) : f.getName();
                println("XREF " + s.getName() + "  <-  " + r.getFromAddress()
                        + "  [" + r.getReferenceType() + "]  fn=" + key);
                c++;
                if (f != null && doneFn.add(f.getName())) {
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
        }
        println("@@@@ DONE");
    }
}