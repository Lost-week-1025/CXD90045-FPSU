import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.*;
import java.util.*;

public class Deep4 extends GhidraScript {
    void dec(String addrStr, FunctionManager fm, DecompInterface di) throws Exception {
        Address a = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(addrStr);
        Function f = fm.getFunctionAt(a);
        if (f == null) { println("!! no func at " + addrStr); return; }
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
    void datum(String addrStr) throws Exception {
        Address a = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(addrStr);
        Memory mem = currentProgram.getMemory();
        byte[] buf = new byte[16];
        int n = mem.getBytes(a, buf);
        StringBuilder sb = new StringBuilder();
        for (int i=0;i<n;i++) sb.append(String.format("%02X ", buf[i]));
        println("DATUM " + addrStr + "  (" + n + " B)  " + sb);
    }
    @Override
    public void run() throws Exception {
        FunctionManager fm = currentProgram.getFunctionManager();
        DecompInterface di = new DecompInterface();
        di.toggleCCode(true); di.toggleSyntaxTree(true);
        if (!di.openProgram(currentProgram)) { println("!! open failed"); return; }
        dec("001496b8", fm, di);   // 真正的开关 (view, 0/1)
        dec("00149676", fm, di);   // 返回状态 0/2/8
        dec("0014934c", fm, di);   // 早期守卫
        dec("0014b764", fm, di);   // 守卫
        dec("00149368", fm, di);   // 守卫
        dec("0014730c", fm, di);   // 守卫
        println("");
        println("@@@@ DATA CONSTANTS @@@@");
        datum("0014b878"); datum("0014b87c");
        println("@@@@ DONE");
    }
}