package org.lionsoul.ip2region;

import org.lionsoul.ip2region.xdb.Searcher;

import java.io.File;
import java.io.IOException;
import java.net.URI;
import java.nio.file.Paths;
import java.util.concurrent.TimeUnit;

public class SearcherTest {
    public static void main(String[] args) throws IOException {
        // 1、创建 searcher 对象
        // 官方
//        String dbPath = new File("./data/ip2region.xdb").getAbsolutePath();
        // 自建xdb文件
        String dbPath = Paths.get("./maker/java/ip2region.xdb").toAbsolutePath().toString();
        Searcher searcher = null;
        try {
            searcher = Searcher.newWithFileOnly(dbPath);
        } catch (IOException e) {
            System.out.printf("failed to create searcher with `%s`: %s\n", dbPath, e);
            return;
        }

        // 2、查询
        String ip = "192.168.0.51";
        try {
            long sTime = System.nanoTime();
            String region = searcher.search(ip);
            long cost = TimeUnit.NANOSECONDS.toMicros((long) (System.nanoTime() - sTime));
            System.out.printf("{region: %s, ioCount: %d, took: %d μs}\n", region, searcher.getIOCount(), cost);
        } catch (Exception e) {
            System.out.printf("failed to search(%s): %s\n", ip, e);
        }

        // 3、关闭资源
        searcher.close();

        // 备注：并发使用，每个线程需要创建一个独立的 searcher 对象单独使用。
    }
}
