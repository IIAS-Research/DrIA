<script setup lang="ts">
import 'vue3-typer/dist/vue-typer.css';

const baiddySubtitles: Array<string> = [
  'Your Doctor in the AI Revolution for Healthcare',
  'Empowering Practitioners with Intelligent AI Analysis',
  'Driving Medical Breakthroughs with Dr IA',
  'Revolutionizing Healthcare with AI Expertise',
  'Transforming Diagnostics with Dr IA’s Insights',
  'Maximizing Medical Data for Better Patient Care',
  "Enhancing Precision Medicine with Dr IA's Intelligence",
  'Leading the Future of AI-Enabled Medical Discoveries',
  'Pioneering AI-Driven Healthcare Solutions',
  'Accelerating Medical Insights with Dr IA',
  'Unlocking the Power of Medical Data Science',
  'Precision Medicine: Powered by AI Intelligence',
  'Innovative AI for Advanced Medical Diagnostics',
  "Elevating Healthcare with Dr IA's Analytics",
  'Advancing Medical Research through AI-Driven Analysis',
  'Unleashing AI for Smarter Healthcare Decisions',
  'Empowering Healthcare Professionals with Dr IA',
  'Shaping the Future of Medical Data Interpretation with AI',
  'Revitalizing Healthcare through AI Innovation',
  'Your Partner in Transforming Medical Data Analysis',
  'AI-Powered Intelligence for Next-Gen Diagnostics',
  'AI-Enhanced Solutions for Better Patient Care',
  'Elevate Your Medical Practice with Dr IA’s Technology',
  'Revolutionary AI for Personalized Healthcare Solutions',
  'Innovate, Analyze, Revolutionize with Dr IA',
  'Dr IA: Your Gateway to Advanced Medical Insights',
  "Reimagine Healthcare with Dr IA’s Data-Driven Approach",
  'Pioneering a New Era in Medical AI Science',
  'Empowering Healthcare Professionals with Intelligent AI Tools',
  'AI-Driven Diagnostics for Superior Patient Outcomes',
  'Transforming Medical Data into Actionable Insights with Dr IA',
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
        Dr.IA
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
