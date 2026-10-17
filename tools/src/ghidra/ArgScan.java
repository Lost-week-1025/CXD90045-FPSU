import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.symbol.*;
import java.util.*;

public class ArgScan extends GhidraScript {
    void scan(String nameFrag, FunctionManager fm, ReferenceManager rm) throws Exception {
        println("");
        println("########## TARGET NAME ~ " + nameFrag);
        SymbolTable st = currentProgram.getSymbolTable();
        List<Symbol> syms = new ArrayList<Symbol>();
        SymbolIterator it = st.getAllSymbols(true);
        while (it.hasNext()) {
            Symbol s = it.next();
            if (s.getName().contains(nameFrag)) syms.add(s);
        }
        for (Symbol s : syms) {
            println("  SYM " + s.getAddress() + " ext=" + s.isExternal()
                    + " " + s.getSymbolType() + " " + s.getName());
            Address a = s.getAddress();
            if (s.isExternal()) continue;
            ReferenceIterator rit = rm.getReferencesTo(a);
            int n = 0;
            while (rit.hasNext() && n < 30) {
                Reference r = rit.next();
                if (!r.getReferenceType().isCall() && !r.getReferenceType().isData()) continue;
                Address from = r.getFromAddress();
                Function cf = fm.getFunctionContaining(from);
                // 回溯最多 5 条指令
                StringBuilder pre = new StringBuilder();
                Instruction ins = getInstructionAt(from);
                if (ins == null) ins = getInstructionContaining(from);
                if (ins != null) {
                    Instruction p = ins.getPrevious();
                    List<String> seq = new ArrayList<String>();
                    for (int i=0;i<5 && p!=null;i++) { seq.add(0, p.getAddress()+" "+p.toString()); p = p.getPrevious(); }
                    for (String x : seq) pre.append(" | ").append(x);
                }
                println("     <- " + from + " [" + r.getReferenceType() + "] in "
                        + (cf==null?"(none)":cf.getName()));
                println("        前序:" + pre);
                n++;
            }
            if (n==0) println("     (无调用引用)");
        }
    }
    @Override
    public void run() throws Exception {
        FunctionManager fm = currentProgram.getFunctionManager();
        ReferenceManager rm = currentProgram.getReferenceManager();
        scan("setTouchable", fm, rm);
        scan("MenuSelectMode", fm, rm);
        println("@@@@ DONE");
    }
}