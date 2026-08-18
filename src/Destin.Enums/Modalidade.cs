namespace Destin.Enums
{
    using System.ComponentModel;

    public enum Modalidade
    {
        [Description("Dia de Sorte")]
        DiaDeSorte = 1,

        [Description("Dupla Sena")]
        DuplaSena = 2,

        Loteca = 3,

        [Description("Loteria Federal")]
        LoteriaFederal = 4,

        [Description("Lotofácil")]
        Lotofacil = 5,

        Lotomania = 6,

        [Description("+Milionária")]
        MaisMilionaria = 7,

        [Description("Mega-Sena")]
        MegaSena = 8,

        Quina = 9,

        [Description("Super Sete")]
        SuperSete = 10,

        Timemania = 11,
    }
}