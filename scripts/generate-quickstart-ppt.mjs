import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const SLIDE = { width: 1280, height: 720 };
const COLORS = {
  ink: "#1f2933",
  muted: "#5f6b7a",
  soft: "#f5f3ea",
  paper: "#fffdf7",
  line: "#ded8c7",
  blue: "#2f80ed",
  yellow: "#ffd43b",
  green: "#2fb36d",
  dark: "#171a20",
};

function parseArgs(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index++) {
    const arg = argv[index];
    if (!arg.startsWith("--")) {
      continue;
    }
    const key = arg.slice(2);
    const value = argv[index + 1] && !argv[index + 1].startsWith("--")
      ? argv[++index]
      : "true";
    result[key] = value;
  }
  return result;
}

async function writeBlob(filePath, blob) {
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

async function readImageBlob(imagePath) {
  const bytes = await fs.readFile(imagePath);
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
}

function imageContentType(fileName) {
  const ext = path.extname(fileName).toLowerCase();
  if (ext === ".gif") {
    return "image/gif";
  }
  if (ext === ".jpg" || ext === ".jpeg") {
    return "image/jpeg";
  }
  return "image/png";
}

function addText(slide, text, position, style = {}) {
  const shape = slide.shapes.add({
    geometry: "textbox",
    position,
    fill: "none",
    line: { style: "solid", fill: "none", width: 0 },
  });
  shape.text = text;
  shape.text.style = {
    typeface: "Malgun Gothic",
    fontSize: style.fontSize ?? 22,
    bold: style.bold ?? false,
    color: style.color ?? COLORS.ink,
    alignment: style.alignment ?? "left",
  };
  return shape;
}

function addRule(slide, left, top, width, color = COLORS.yellow) {
  slide.shapes.add({
    geometry: "rect",
    position: { left, top, width, height: 6 },
    fill: color,
    line: { style: "solid", fill: color, width: 0 },
  });
}

function addLabel(slide, text, left, top, color = COLORS.blue) {
  const label = slide.shapes.add({
    geometry: "roundRect",
    position: { left, top, width: 104, height: 36 },
    fill: color,
    line: { style: "solid", fill: color, width: 0 },
    borderRadius: "rounded-xl",
  });
  label.text = text;
  label.text.style = {
    typeface: "Malgun Gothic",
    fontSize: 18,
    bold: true,
    color: "#ffffff",
    alignment: "center",
  };
}

function addSlideTitle(slide, title, subtitle = "") {
  addText(slide, title, { left: 72, top: 54, width: 790, height: 56 }, {
    fontSize: 42,
    bold: true,
  });
  if (subtitle) {
    addText(slide, subtitle, { left: 74, top: 116, width: 760, height: 34 }, {
      fontSize: 20,
      color: COLORS.muted,
    });
  }
  addRule(slide, 72, 160, 120);
}

function addFooter(slide, page) {
  addText(slide, "McpForUnity Agent Quickstart", { left: 72, top: 668, width: 360, height: 24 }, {
    fontSize: 15,
    color: "#8a9380",
  });
  addText(slide, String(page).padStart(2, "0"), { left: 1150, top: 662, width: 60, height: 34 }, {
    fontSize: 22,
    bold: true,
    color: "#8a9380",
    alignment: "right",
  });
}

function addBullets(slide, items, left, top, width, fontSize = 23, gap = 52) {
  items.forEach((item, index) => {
    const y = top + index * gap;
    slide.shapes.add({
      geometry: "ellipse",
      position: { left, top: y + 9, width: 12, height: 12 },
      fill: item.color ?? COLORS.yellow,
      line: { style: "solid", fill: item.color ?? COLORS.yellow, width: 0 },
    });
    addText(slide, item.text, { left: left + 28, top: y, width, height: 40 }, {
      fontSize,
      color: item.textColor ?? (item.muted ? COLORS.muted : COLORS.ink),
      bold: item.bold ?? false,
    });
  });
}

function addStepList(slide, steps, left, top, width, gap = 78) {
  steps.forEach((step, index) => {
    const y = top + index * gap;
    slide.shapes.add({
      geometry: "ellipse",
      position: { left, top: y + 2, width: 34, height: 34 },
      fill: COLORS.yellow,
      line: { style: "solid", fill: COLORS.yellow, width: 0 },
    });
    addText(slide, String(step.no), { left, top: y + 3, width: 34, height: 28 }, {
      fontSize: 19,
      bold: true,
      color: COLORS.dark,
      alignment: "center",
    });
    addText(slide, step.title, { left: left + 48, top: y, width, height: 30 }, {
      fontSize: 24,
      bold: true,
      color: COLORS.ink,
    });
    if (step.detail) {
      addText(slide, step.detail, { left: left + 48, top: y + 34, width, height: 28 }, {
        fontSize: 18,
        color: COLORS.muted,
      });
    }
  });
}

function addWhyBlock(slide, text, left = 78, top = 212, width = 430) {
  addText(slide, "왜 필요한가요?", { left, top, width, height: 28 }, {
    fontSize: 22,
    bold: true,
    color: COLORS.ink,
  });
  addText(slide, text, { left, top: top + 34, width, height: 58 }, {
    fontSize: 18,
    color: COLORS.muted,
  });
  addRule(slide, left, top + 104, 72, COLORS.yellow);
}

async function addImage(slide, assetDir, fileName, position, alt, fit = "contain") {
  const imagePath = path.join(assetDir, fileName);
  const blob = await readImageBlob(imagePath);
  slide.images.add({
    blob,
    contentType: imageContentType(fileName),
    alt,
    fit,
    position,
    geometry: "roundRect",
    borderRadius: "rounded-xl",
  });
}

function addSurface(slide, position, fill = COLORS.paper) {
  slide.shapes.add({
    geometry: "roundRect",
    position,
    fill,
    line: { style: "solid", fill: COLORS.line, width: 1 },
    borderRadius: "rounded-2xl",
    shadow: "shadow-sm",
  });
}

async function addScreenshotSlide(slide, page, title, subtitle, why, imageName, imageAlt, bullets, imagePosition) {
  slide.background.fill = COLORS.soft;
  addSlideTitle(slide, title, subtitle);
  addSurface(slide, imagePosition);
  await addImage(slide, paths.assetDir, imageName, {
    left: imagePosition.left + 14,
    top: imagePosition.top + 14,
    width: imagePosition.width - 28,
    height: imagePosition.height - 28,
  }, imageAlt);
  addWhyBlock(slide, why);
  addStepList(slide, bullets, 78, 350, 410, 70);
  addFooter(slide, page);
}

const args = parseArgs(process.argv.slice(2));
const repoRoot = path.resolve(args["repo-root"] ?? process.cwd());
const paths = {
  repoRoot,
  assetDir: path.resolve(args["asset-dir"] ?? path.join(repoRoot, "artifacts", "docs-build", "assets", "quickstart-ko")),
  out: path.resolve(args.out ?? path.join(repoRoot, "docs", "McpForUnityAgent-quickstart-ko.pptx")),
  previewDir: path.resolve(args["preview-dir"] ?? path.join(repoRoot, "artifacts", "docs-build", "preview")),
};

async function main() {
  await fs.mkdir(path.dirname(paths.out), { recursive: true });
  await fs.mkdir(paths.previewDir, { recursive: true });

  const presentation = Presentation.create({ slideSize: SLIDE });

  const slide1 = presentation.slides.add();
  slide1.background.fill = COLORS.dark;
  addText(slide1, "AI가 Unity를\n직접 다루게\n해보세요", {
    left: 72,
    top: 74,
    width: 700,
    height: 230,
  }, { fontSize: 61, bold: true, color: "#ffffff" });
  addRule(slide1, 76, 328, 180, COLORS.yellow);
  addText(slide1, "McpForUnity Agent", {
    left: 76,
    top: 370,
    width: 620,
    height: 42,
  }, { fontSize: 30, color: "#dbe4ee", bold: true });
  addText(slide1, "Unity MCP는 AI가 씬을 보고, 에셋을 만들고,\n에디터 작업을 이어가게 해줍니다.", {
    left: 76,
    top: 428,
    width: 720,
    height: 70,
  }, { fontSize: 23, color: "#b7c2d0" });
  addText(slide1, "Agent로 설치와 연결을 빠르게 끝냅니다.", {
    left: 78,
    top: 574,
    width: 780,
    height: 36,
  }, { fontSize: 24, color: "#ffffff", bold: true });
  addSurface(slide1, { left: 684, top: 116, width: 524, height: 330 }, "#222832");
  await addImage(slide1, paths.assetDir, "00-unity-mcp-building-scene.gif", {
    left: 702,
    top: 134,
    width: 488,
    height: 274,
  }, "MCP for Unity building a scene demo", "cover");
  addText(slide1, "Demo: CoplayDev/unity-mcp README", {
    left: 708,
    top: 414,
    width: 470,
    height: 22,
  }, { fontSize: 14, color: "#8fa0b4", alignment: "right" });

  const slide2 = presentation.slides.add();
  slide2.background.fill = COLORS.paper;
  addSlideTitle(slide2, "하고 싶은 일은 이미 분명합니다", "문제는 AI 도구가 Unity 안을 그냥 볼 수 없다는 점입니다.");
  const cardTop = 232;
  const cardWidth = 330;
  addSurface(slide2, { left: 76, top: cardTop, width: cardWidth, height: 300 }, "#ffffff");
  addSurface(slide2, { left: 476, top: cardTop, width: cardWidth, height: 300 }, "#ffffff");
  addSurface(slide2, { left: 876, top: cardTop, width: cardWidth, height: 300 }, "#fff9db");
  addText(slide2, "지금 씬을\n같이 보고", { left: 112, top: cardTop + 26, width: 260, height: 68 }, {
    fontSize: 28,
    bold: true,
    alignment: "center",
  });
  addText(slide2, "“왜 이 오브젝트가\n안 보이지?”\n“상태 좀 확인해줘”", {
    left: 114,
    top: cardTop + 116,
    width: 256,
    height: 116,
  }, { fontSize: 22, color: COLORS.muted, alignment: "center" });
  addText(slide2, "반복 작업은\n맡기고", { left: 512, top: cardTop + 26, width: 260, height: 68 }, {
    fontSize: 28,
    bold: true,
    alignment: "center",
  });
  addText(slide2, "태그, 위치,\n프리팹, 설정 확인처럼\n손이 많이 가는 일", {
    left: 514,
    top: cardTop + 116,
    width: 256,
    height: 142,
  }, { fontSize: 22, color: COLORS.muted, alignment: "center" });
  addText(slide2, "시작 장벽은\n낮추고", { left: 912, top: cardTop + 26, width: 260, height: 68 }, {
    fontSize: 27,
    bold: true,
    alignment: "center",
  });
  addText(slide2, "설치하고\nAI 도구와 Unity를\n바로 이어서\n작업 시작", {
    left: 914,
    top: cardTop + 104,
    width: 256,
    height: 142,
  }, { fontSize: 22, color: COLORS.ink, alignment: "center" });
  addText(slide2, "McpForUnity Agent는 Unity MCP를 처음 켜는 순간의 장벽을 낮춥니다.", {
    left: 130,
    top: 590,
    width: 980,
    height: 36,
  }, { fontSize: 25, bold: true, color: COLORS.ink, alignment: "center" });
  addFooter(slide2, 2);

  const slide3 = presentation.slides.add();
  slide3.background.fill = COLORS.paper;
  addSlideTitle(slide3, "Agent는 시작 버튼에 가깝습니다", "Unity MCP가 할 수 있는 일을 설치와 연결에서 막히지 않게 합니다.");
  const flowTop = 238;
  const flowBox = { width: 220, height: 168 };
  addSurface(slide3, { left: 66, top: flowTop, width: flowBox.width, height: flowBox.height }, "#ffffff");
  addSurface(slide3, { left: 368, top: flowTop, width: flowBox.width, height: flowBox.height }, "#fff9db");
  addSurface(slide3, { left: 670, top: flowTop, width: flowBox.width, height: flowBox.height }, "#fff9db");
  addSurface(slide3, { left: 972, top: flowTop, width: flowBox.width, height: flowBox.height }, "#ffffff");
  addText(slide3, "AI 도구", { left: 90, top: flowTop + 34, width: 172, height: 34 }, { fontSize: 29, bold: true, alignment: "center" });
  addText(slide3, "질문하고\n작업 요청", { left: 90, top: flowTop + 92, width: 172, height: 58 }, { fontSize: 20, color: COLORS.muted, alignment: "center" });
  addText(slide3, "Agent", { left: 392, top: flowTop + 34, width: 172, height: 34 }, { fontSize: 29, bold: true, alignment: "center" });
  addText(slide3, "설치와 실행을\n트레이에서 관리", { left: 392, top: flowTop + 92, width: 172, height: 58 }, { fontSize: 20, color: COLORS.ink, alignment: "center" });
  addText(slide3, "Unity\n플러그인", { left: 694, top: flowTop + 26, width: 172, height: 66 }, { fontSize: 28, bold: true, alignment: "center" });
  addText(slide3, "프로젝트 안에서\nAI 요청 수행", { left: 694, top: flowTop + 104, width: 172, height: 48 }, { fontSize: 20, color: COLORS.ink, alignment: "center" });
  addText(slide3, "Unity\n프로젝트", { left: 996, top: flowTop + 26, width: 172, height: 66 }, { fontSize: 28, bold: true, alignment: "center" });
  addText(slide3, "씬, 오브젝트,\n에디터 작업", { left: 996, top: flowTop + 104, width: 172, height: 48 }, { fontSize: 20, color: COLORS.muted, alignment: "center" });
  for (const left of [298, 600, 902]) {
    addText(slide3, "→", { left, top: flowTop + 54, width: 48, height: 58 }, {
      fontSize: 44,
      bold: true,
      color: COLORS.blue,
      alignment: "center",
    });
  }
  slide3.shapes.add({
    geometry: "roundRect",
    position: { left: 126, top: 502, width: 1028, height: 88 },
    fill: COLORS.dark,
    line: { style: "solid", fill: COLORS.dark, width: 0 },
    borderRadius: "rounded-2xl",
  });
  addText(slide3, "플러그인은 왜 필요한가요?", { left: 168, top: 520, width: 360, height: 34 }, {
    fontSize: 24,
    bold: true,
    color: "#ffffff",
  });
  addText(slide3, "AI가 실제 프로젝트 안을 읽고 작업하려면 Unity 쪽에도 연결 부품이 있어야 합니다.", {
    left: 168,
    top: 556,
    width: 930,
    height: 28,
  }, { fontSize: 21, color: "#dbe4ee" });
  addFooter(slide3, 3);

  await addScreenshotSlide(
    presentation.slides.add(),
    4,
    "1. Agent 설치",
    "연결 서버를 Windows 트레이에서 관리할 앱을 설치합니다.",
    "Agent가 있어야 터미널 명령 대신 버튼으로 연결 서버를 켜고 끌 수 있습니다.",
    "02-install.png",
    "McpForUnity Agent install window",
    [
      { no: 1, title: "기본값 유지", detail: "경로와 인자는 바꾸지 않습니다." },
      { no: 2, title: "Setup Dependencies", detail: "uv와 Git 상태를 확인합니다." },
      { no: 3, title: "설치", detail: "트레이 앱을 등록합니다." },
    ],
    { left: 556, top: 196, width: 620, height: 388 }
  );

  await addScreenshotSlide(
    presentation.slides.add(),
    5,
    "2. 실행 준비 확인",
    "Agent가 서버를 켜고 Unity 플러그인을 가져올 준비를 확인합니다.",
    "이 준비가 빠지면 앱은 설치돼도 연결 서버가 시작되지 않거나 플러그인을 가져오지 못합니다.",
    "03-dependencies.png",
    "Dependencies window",
    [
      { no: 1, title: "상태 확인", detail: "Missing이면 설치가 필요합니다." },
      { no: 2, title: "Install Missing", detail: "빠진 항목만 설치합니다." },
      { no: 3, title: "Refresh", detail: "설치 후 다시 확인합니다." },
    ],
    { left: 546, top: 230, width: 610, height: 260 }
  );

  const slide6 = presentation.slides.add();
  slide6.background.fill = COLORS.soft;
  addSlideTitle(slide6, "3. Unity 프로젝트에 플러그인 추가", "AI가 Unity 안을 볼 수 있게 프로젝트 쪽 연결 부품을 넣습니다.");
  addWhyBlock(slide6, "이 단계를 건너뛰면 AI 도구는 씬과 오브젝트를 읽거나 다룰 수 없습니다.");
  addStepList(slide6, [
    { no: 1, title: "프로젝트 폴더", detail: "Assets가 있는 폴더입니다." },
    { no: 2, title: "찾아보기", detail: "폴더를 직접 고릅니다." },
    { no: 3, title: "Install", detail: "Unity 패키지를 추가합니다." },
  ], 78, 350, 410, 70);
  addSurface(slide6, { left: 536, top: 274, width: 650, height: 190 });
  await addImage(slide6, paths.assetDir, "06-unity-plugin.png", {
    left: 550,
    top: 292,
    width: 622,
    height: 154,
  }, "Unity plugin install dialog");
  addFooter(slide6, 6);

  await addScreenshotSlide(
    presentation.slides.add(),
    7,
    "4. AI 도구에 연결 주소 알려주기",
    "Codex, Cursor, VS Code가 Agent를 찾아가게 합니다.",
    "이 단계가 없으면 AI 도구는 Unity 연결 통로가 어디 있는지 몰라서 사용할 수 없습니다.",
    "05-config-client.png",
    "Client configuration window",
    [
      { no: 1, title: "Client 탭", detail: "연결할 도구 목록을 봅니다." },
      { no: 2, title: "한 번에 설정", detail: "감지된 클라이언트를 설정합니다." },
      { no: 3, title: "Configured 확인", detail: "표시되면 준비 완료입니다." },
    ],
    { left: 532, top: 188, width: 680, height: 424 }
  );

  const slide8 = presentation.slides.add();
  slide8.background.fill = COLORS.dark;
  addText(slide8, "설치 후에는\n이 상태만\n유지하면 됩니다", { left: 72, top: 78, width: 630, height: 190 }, {
    fontSize: 54,
    bold: true,
    color: "#ffffff",
  });
  addRule(slide8, 76, 294, 160, COLORS.green);
  addBullets(slide8, [
    { text: "Unity를 열기 전에 Agent가 켜져 있는지 확인", color: COLORS.green, textColor: "#dbe4ee" },
    { text: "문제가 있으면 트레이에서 Restart", color: COLORS.yellow, textColor: "#dbe4ee" },
    { text: "계속 안 되면 Console 로그 확인", color: COLORS.blue, textColor: "#dbe4ee" },
  ], 84, 362, 650, 26, 62);
  addSurface(slide8, { left: 850, top: 142, width: 250, height: 330 }, "#222832");
  await addImage(slide8, paths.assetDir, "01-tray-menu.png", {
    left: 896,
    top: 178,
    width: 158,
    height: 250,
  }, "Tray menu");
  addText(slide8, "클라이언트에서 unityMCP가 보이면 사용할 준비가 된 상태입니다.", {
    left: 78,
    top: 610,
    width: 920,
    height: 36,
  }, { fontSize: 23, color: "#ffffff" });
  addFooter(slide8, 8);

  for (const [index, slide] of presentation.slides.items.entries()) {
    const stem = `slide-${String(index + 1).padStart(2, "0")}`;
    const png = await presentation.export({ slide, format: "png", scale: 1 });
    await writeBlob(path.join(paths.previewDir, `${stem}.png`), png);
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(path.join(paths.previewDir, `${stem}.layout.json`), await layout.text());
  }

  const montage = await presentation.export({ format: "webp", montage: true, scale: 1 });
  await writeBlob(path.join(paths.previewDir, "deck-montage.webp"), montage);

  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(paths.out);
  console.log(paths.out);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
