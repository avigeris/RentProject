using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;

namespace RentProject
{
    enum statusCodeOptionSet
    {
        Created = 1,
        Confirmed = 735370000,
        Renting = 735370001,
        Returned = 2,
        Canceled = 735370002
    }
    class Program
    {   //random date/time between two days
        public static DateTime RandomDateTime(DateTime dateTimeFrom, DateTime dateTimeTo, Random rnd)
        {

            TimeSpan timeSpan = dateTimeTo - dateTimeFrom;
            TimeSpan newSpan = new TimeSpan(0, rnd.Next(0, (int)timeSpan.TotalMinutes), 0);
            DateTime newDate = dateTimeFrom + newSpan;

            //Return the value
            return newDate;
        }

        public static KeyValuePair<statusCodeOptionSet, double> probalityBasedRandom (List<KeyValuePair<statusCodeOptionSet, double>> elements, Random rnd)
        {
            double diceRoll = rnd.NextDouble();
            double cumulative = 0.0;
            for (int j = 0; j < elements.Count; j++)
            {
                cumulative += elements[j].Value;
                if (diceRoll < cumulative)
                {
                    return elements[j];
                }
            }
            return elements[1];
        }

        static void Main(string[] args)
        {
            //connection
            string connectionString = "";
            CrmServiceClient service = new CrmServiceClient(connectionString);
            OrganizationServiceContext context = new OrganizationServiceContext(service);
            
            //get some data
            var carClassRecords = (from carClass in context.CreateQuery("crc6f_car_class") select carClass).ToList();
            var customerRecords = (from customer in context.CreateQuery("contact") where customer["ownerid"] == "94D249CC-60E1-EB11-BACB-000D3A4AF503" select customer).ToList();

            DateTime start = new DateTime(2019, 1, 1);
            DateTime end = new DateTime(2020, 12, 31);
            var rnd = new Random();

            //trying one way to store location
            Dictionary<Int32, string> locationOptionSet = new Dictionary<Int32, string> {
                    { 1, "Airport" },
                    { 2, "City Center" },
                    { 3, "Office" }
                };

            //status code and their probability
            List<KeyValuePair<statusCodeOptionSet, double>> statusCodeProbability = new List<KeyValuePair<statusCodeOptionSet, double>> {
                    new KeyValuePair<statusCodeOptionSet, double>(statusCodeOptionSet.Created, 0.05),
                    new KeyValuePair<statusCodeOptionSet, double>(statusCodeOptionSet.Confirmed, 0.05),
                    new KeyValuePair<statusCodeOptionSet, double>(statusCodeOptionSet.Renting, 0.05),
                    new KeyValuePair<statusCodeOptionSet, double>(statusCodeOptionSet.Returned, 0.75),
                    new KeyValuePair<statusCodeOptionSet, double>(statusCodeOptionSet.Canceled, 0.1)
                };

            //create rent records
            for (int i = 0; i < 40000; i++)
            {
                Console.WriteLine("Rent: " + i.ToString());
                Entity rent = new Entity("crc6f_rent");
                rent["crc6f_name"] = "Sample " + i.ToString();

                DateTime reservedPickup = RandomDateTime(start, end, rnd);
                rent["crc6f_reservedpickup"] = reservedPickup;

                DateTime reservedHandover = RandomDateTime(reservedPickup, reservedPickup.AddDays(30), rnd);
                rent["crc6f_reserved_handover"] = reservedHandover;

                //set lookup car class field
                var selectedCarClass = carClassRecords[rnd.Next(carClassRecords.Count)];
                var carsClass = selectedCarClass.Attributes["crc6f_car_classid"];
                EntityReference selectedCarClassref = selectedCarClass.ToEntityReference();
                rent["crc6f_car_class"] = selectedCarClassref;

                //set car field
                var carsInClassRecords = (from carInClass in context.CreateQuery("crc6f_car") where carInClass["crc6f_car_class"] == carsClass  select carInClass).ToList();
                var selectedCar = carsInClassRecords[rnd.Next(carsInClassRecords.Count)];
                EntityReference selectedCarref = selectedCar.ToEntityReference();
                rent["crc6f_car"] = selectedCarref;

                //set customer field
                var selectedCustomer = customerRecords[rnd.Next(customerRecords.Count)];
                EntityReference selectedCustomerref = selectedCustomer.ToEntityReference();
                rent["crc6f_customer"] = selectedCustomerref;

                //set locations
                var pickupLocation = locationOptionSet.ElementAt(rnd.Next(locationOptionSet.Count()));
                rent["crc6f_pickuplocation"] = new OptionSetValue(pickupLocation.Key);
                var returnLocation = locationOptionSet.ElementAt(rnd.Next(locationOptionSet.Count()));
                rent["crc6f_return_location"] = new OptionSetValue(returnLocation.Key);
                
                //pick status and set statecode
                var selectedStatusCode = probalityBasedRandom(statusCodeProbability, rnd).Key;
                if (selectedStatusCode == statusCodeOptionSet.Returned || selectedStatusCode == statusCodeOptionSet.Canceled)
                {
                    rent["statecode"] = new OptionSetValue(1);
                }
                else
                {
                    rent["statecode"] = new OptionSetValue(0);
                }
                rent["statuscode"] = new OptionSetValue(((int)selectedStatusCode));

                //create pickup report
                if (selectedStatusCode == statusCodeOptionSet.Renting || selectedStatusCode == statusCodeOptionSet.Returned)
                {
                    Entity pickupReport = new Entity("crc6f_cartransferreport");
                    pickupReport["crc6f_name"] = "Sample pickup " + i.ToString();
                    pickupReport["crc6f_car"] = selectedCarref;
                    pickupReport["crc6f_type"] = false;
                    pickupReport["crc6f_date"] = reservedPickup;

                    double probability = rnd.NextDouble();
                    if (probability <= 0.05)
                    {
                        pickupReport["crc6f_damages"] = true;
                        pickupReport["crc6f_damage_description"] = "damage";
                    }

                    Guid pickupReportGuid = service.Create(pickupReport);
                    Console.WriteLine(pickupReportGuid.ToString());
                    rent["crc6f_pickup_report"] = new EntityReference("crc6f_cartransferreport", pickupReportGuid);
                }

                //create return report
                if (selectedStatusCode == statusCodeOptionSet.Returned)
                {
                    Entity returnReport = new Entity("crc6f_cartransferreport");
                    returnReport["crc6f_name"] = "Sample return " + i.ToString();
                    returnReport["crc6f_car"] = selectedCarref;
                    returnReport["crc6f_type"] = true;
                    returnReport["crc6f_date"] = reservedHandover;

                    double probability = rnd.NextDouble();
                    if (probability <= 0.05)
                    {
                        returnReport["crc6f_damages"] = true;
                        returnReport["crc6f_damage_description"] = "damage";
                    }

                    Guid returnReportGuid = service.Create(returnReport);
                    Console.WriteLine(returnReportGuid.ToString());
                    rent["crc6f_return_report"] = new EntityReference("crc6f_cartransferreport", returnReportGuid);
                }

                //set paid field
                double paidProbability = rnd.NextDouble();
                if (selectedStatusCode == statusCodeOptionSet.Confirmed && paidProbability < 0.9)
                {
                    rent["crc6f_paid"] = true;
                }

                if (selectedStatusCode == statusCodeOptionSet.Renting && paidProbability < 0.999)
                {
                    rent["crc6f_paid"] = true;
                }

                if (selectedStatusCode == statusCodeOptionSet.Returned && paidProbability < 0.9998)
                {
                    rent["crc6f_paid"] = true;
                }

                //set price field
                decimal moneyValue = ((Money)selectedCarClass.Attributes["crc6f_price"]).Value*(reservedHandover.Date-reservedPickup.Date).Days;
                if (pickupLocation.Value != "Office")
                    moneyValue = moneyValue + 100;
                if (returnLocation.Value != "Office")
                    moneyValue = moneyValue + 100;
                rent["crc6f_price"] = new Money((decimal)moneyValue);

                Guid guid = service.Create(rent);
                Console.WriteLine(guid.ToString());
            }


            Console.Read();
        }
    }
}
