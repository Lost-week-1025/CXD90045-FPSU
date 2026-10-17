import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.util.*;

public class LMode2 extends GhidraScript {
    @Override
    public void run() throws Exception {
        FunctionManager fm = currentProgram.getFunctionManager();
        SymbolTable st = currentProgram.getSymbolTable();
        DecompInterface di = new DecompInterface();
        di.toggleCCode(true); di.toggleSyntaxTree(true);
        if (!di.openProgram(currentProgram)) { println("!! open failed"); return; }
        println("@@@@ PROGRAM " + currentProgram.getName());

        String[] want = {"LayoutMode","LayoutToggle","SetLayoutToggleTable"};
        List<Symbol> found = new ArrayList<Symbol>();
        SymbolIterator it = st.getAllSymbols(true);
        while (it.hasNext()) {
            Symbol s = it.next();
            String n = s.getName();
            for (String w : want) if (n.contains(w)) { found.add(s); break; }
        }
        println("匹配符号 " + found.size());
        Set<String> done = new LinkedHashSet<String>();
        for (Symbol s : found) {
            println("  SYM " + s.getAddress() + " ext=" + s.isExternal() + " " + s.getName());
            if (s.isExternal()) continue;
            Function f = fm.getFunctionAt(s.getAddress());
            if (f == null) f = fm.getFunctionContaining(s.getAddress());
            if (f == null) continue;
            if (!done.add(f.getName())) continue;
            println("");
            println("============ " + f.getName() + " @ " + f.getEntryPoint()
                    + " size=" + f.getBody().getNumAddresses() + " ============");
            try {
                DecompileResults dr = di.decompileFunction(f, 90, monitor);
                if (dr != null && dr.decompileCompleted() && dr.getDecompiledFunction() != null)
                    println(dr.getDecompiledFunction().getC());
                else println("(failed)");
            } catch (Throwable t) { println("(exception " + t + ")"); }
        }
        println("@@@@ DONE");
    }
}