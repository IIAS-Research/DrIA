<script setup lang="ts">
import 'vue3-typer/dist/vue-typer.css';

const baiddySubtitles: Array<string> = [
  'Your Doctor in the AI Revolution for Healthcare',
  'Empowering Practitioners with Intelligent AI Analysis',
  'Driving Medical Breakthroughs with DrIA',
  'Revolutionizing Healthcare with AI Expertise',
  'Transforming Diagnostics with DrIA’s Insights',
  'Maximizing Medical Data for Better Patient Care',
  "Enhancing Precision Medicine with DrIA's Intelligence",
  'Leading the Future of AI-Enabled Medical Discoveries',
  'Pioneering AI-Driven Healthcare Solutions',
  'Accelerating Medical Insights with DrIA',
  'Unlocking the Power of Medical Data Science',
  'Precision Medicine: Powered by AI Intelligence',
  'Innovative AI for Advanced Medical Diagnostics',
  "Elevating Healthcare with DrIA's Analytics",
  'Advancing Medical Research through AI-Driven Analysis',
  'Unleashing AI for Smarter Healthcare Decisions',
  'Empowering Healthcare Professionals with DrIA',
  'Shaping the Future of Medical Data Interpretation with AI',
  'Revitalizing Healthcare through AI Innovation',
  'Your Partner in Transforming Medical Data Analysis',
  'AI-Powered Intelligence for Next-Gen Diagnostics',
  'AI-Enhanced Solutions for Better Patient Care',
  'Elevate Your Medical Practice with DrIA’s Technology',
  'Revolutionary AI for Personalized Healthcare Solutions',
  'Innovate, Analyze, Revolutionize with DrIA',
  'DrIA: Your Gateway to Advanced Medical Insights',
  'Reimagine Healthcare with DrIA’s Data-Driven Approach',
  'Pioneering a New Era in Medical AI Science',
  'Empowering Healthcare Professionals with Intelligent AI Tools',
  'AI-Driven Diagnostics for Superior Patient Outcomes',
  'Transforming Medical Data into Actionable Insights with DrIA',
];

</script>

<template>
  <div>
    <div
      class="flex q-mx-auto flex-center"
    >
      <img
        src="/images/baiddy_logo.svg"
        height="60"
      >
      <p
        class="main_title q-ml-md"
        style="font-size: 60px;"
      >
        DrIA
      </p>
    </div>
    <VueTyper
      class="full-width q-mx-auto"
      :text="baiddySubtitles"
      :shuffle="true"
      :pre-erase-delay="3000"
      erase-style="backspace"
      :erase-delay="35"
      style="display: block; width: 100%;"
    />
  </div>
</template>

<style scoped>
.main_title {
    font-family: 'pixel';
    color: #6EC3C2;
}

@font-face {
    font-family: "pixel";
    src: url("/fonts/pixel_font.ttf");
}

</style>
